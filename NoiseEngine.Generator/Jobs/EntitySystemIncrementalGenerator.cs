﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoiseEngine.Generator.Jobs;

[Generator]
public class EntitySystemIncrementalGenerator : IIncrementalGenerator {

    private const string SystemFullName = "NoiseEngine.Jobs2.EntitySystem";
    private const string AffectiveSystemFullName = "NoiseEngine.Jobs2.IAffectiveSystem";
    private const string AffectiveComponentFullName = "NoiseEngine.Jobs2.IAffectiveComponent";
    private const string EntityFullName = "NoiseEngine.Jobs2.Entity";
    private const string SystemCommandsFullName = "NoiseEngine.Jobs2.SystemCommands";
    private const string InternalInterfacesFullName = "NoiseEngine.Jobs2.Internal.NoiseEngineInternal_DoNotUse";
    private const string InternalNormalSystemFullName = InternalInterfacesFullName + ".INormalEntitySystem";
    private const string InternalAffectiveSystemFullName = InternalInterfacesFullName + ".IAffectiveEntitySystem";
    private const string InternalMethodObsoleteMessage =
        "This method is internal and is not part of the API. Do not use.";

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        IncrementalValuesProvider<ClassDeclarationSyntax> systems = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (x, _) => x is ClassDeclarationSyntax,
                static (context, _) => {
                    if (
                        context.SemanticModel.GetDeclaredSymbol(context.Node) is not ITypeSymbol typeSymbol ||
                        typeSymbol.BaseType is null ||
                        typeSymbol.BaseType.ToDisplayString() != SystemFullName
                    ) {
                        return null!;
                    }

                    return (ClassDeclarationSyntax)context.Node;
                }
            ).Where(static x => x is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)>
            compilationAndSystems = context.CompilationProvider.Combine(systems.Collect());

        context.RegisterSourceOutput(compilationAndSystems, (ctx, source) => {
            object locker = new object();
            Parallel.ForEach(source.Item2, system => {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("// <auto-generated />");
                builder.AppendLine();

                Generate(source.Item1, builder, system);

                string result = builder.ToString();
                string systemName = $"{
                    system.ParentNodes().OfType<BaseNamespaceDeclarationSyntax>().First().Name.GetText()
                }.{system.Identifier.Text}.generated.cs";

                lock (locker)
                    ctx.AddSource(systemName, result);
            });
        });
    }

    private void Generate(Compilation compilation, StringBuilder builder, ClassDeclarationSyntax system) {
        if (GeneratorHelpers.AssertNotUsingInternalThings(builder, system))
            return;

        if (!system.Modifiers.Any(x => x.ValueText == "partial")) {
            builder.AppendLine("#error EntitySystem must be partial.");
            return;
        }

        bool isAffective = false;
        int genericAffectiveCount = 0;
        string? genericAffectiveString = null;

        foreach (INamedTypeSymbol i in system.GetDeclaredSymbol<ITypeSymbol>(compilation).AllInterfaces) {
            string s = i.ToDisplayString();
            if (s == InternalNormalSystemFullName) {
                builder.Append("#error System defined `").Append(InternalNormalSystemFullName)
                    .AppendLine("` which is not allowed.");
                return;
            } else if (s == InternalAffectiveSystemFullName) {
                builder.Append("#error System defined `").Append(InternalAffectiveSystemFullName)
                    .AppendLine("` which is not allowed.");
                return;
            } else if (!s.StartsWith(AffectiveSystemFullName)) {
                continue;
            }

            isAffective = true;
            if (s.Length > AffectiveSystemFullName.Length && i.TypeParameters.Length != 0) {
                genericAffectiveCount++;
                genericAffectiveString = s;
            }
        }

        string[] inherited;
        if (isAffective) {
            if (genericAffectiveCount == 0)
                builder.AppendLine("#error Affective system must define generic IAffectiveSystem.");
            else if (genericAffectiveCount > 1)
                builder.AppendLine("#error Affective system must define only one generic IAffectiveSystem.");

            inherited = new string[] { InternalAffectiveSystemFullName };
        } else {
            inherited = new string[] { InternalNormalSystemFullName };
        }

        GeneratorHelpers.GenerateUsings(builder, system);
        builder.AppendLine("#nullable enable");
        builder.AppendLine("#pragma warning disable 612, 618");
        GeneratorHelpers.GenerateNamespaceWithType(builder, system, null, inherited);
        builder.AppendLine();

        if (genericAffectiveString is not null) {
            builder.AppendIndentation(2).Append("static System.Type[] ").Append(InternalAffectiveSystemFullName)
                .AppendLine(".AffectiveComponents { get; } = new System.Type[] {");

            foreach (string type in GeneratorHelpers.GetGenerics(genericAffectiveString))
                builder.AppendIndentation(3).Append("typeof(").Append(type).AppendLine("),");
            builder.Remove(builder.Length - 1 - Environment.NewLine.Length, 1 + Environment.NewLine.Length)
                .AppendLine();

            builder.AppendIndentation(2).AppendLine("};").AppendLine();
        }

        MethodDeclarationSyntax[] methods = system.ChildNodes().OfType<MethodDeclarationSyntax>()
            .Where(x => x.Identifier.Text == "OnUpdateEntity").ToArray();
        if (methods.Length > 1)
            builder.AppendLine("#warning Multiple `OnUpdateEntity` methods in one system, only first will be used.");

        MethodDeclarationSyntax? onUpdateEntity = methods.FirstOrDefault();
        if (
            onUpdateEntity is not null && onUpdateEntity.ChildNodes().First(x => x is PredefinedTypeSyntax)
                .GetSymbol<INamedTypeSymbol>(compilation).ToDisplayString() != "void"
        ) {
            builder.AppendLine("#error `OnUpdateEntity` method must return `void`.");
        }

        (string parameterType, bool isRef, bool isIn, bool isOut, bool isAffective)[] parameters =
            GenerateInitializeMethod(compilation, builder, onUpdateEntity);

        foreach ((string parameterType, bool isRef, bool isIn, bool isOut, _) in parameters) {
            if (isIn || isOut) {
                builder.Append("#error Parameter `").Append(parameterType)
                    .AppendLine("` has `in` or `out` keyword which is not allowed in `OnUpdateEntity`.");
            }

            if (isRef && (parameterType == EntityFullName || parameterType == SystemCommandsFullName)) {
                builder.Append("#error Parameter `").Append(parameterType)
                    .AppendLine("` has `ref` keyword which is not allowed in for not component parameter.");
            }
        }

        foreach (
            string parameter in parameters.Select(x => x.parameterType).GroupBy(x => x).Where(x => x.Count() > 1)
                .SelectMany(x => x).Distinct()
        ) {
            builder.Append("#warning Parameter `").Append(parameter)
                .AppendLine("` is used more than once in `OnUpdateEntity`.");
        }

        if (onUpdateEntity is not null)
            GenerateSystemExecutionMethod(builder, onUpdateEntity, parameters);

        builder.AppendIndentation().AppendLine("}");
        builder.AppendLine("}");
    }

    private (string parameterType, bool isRef, bool isIn, bool isOut, bool isAffective)[] GenerateInitializeMethod(
        Compilation compilation, StringBuilder builder, MethodDeclarationSyntax? onUpdateEntity
    ) {
        builder.AppendIndentation(2)
            .Append("[System.Obsolete(\"").Append(InternalMethodObsoleteMessage).AppendLine("\")]")
            .AppendIndentation(2).Append("protected override void ").Append(GeneratorConstants.InternalThings)
            .AppendLine("_Initialize() {");

        (string, bool, bool, bool, bool)[] parameters;
        bool componentWriteAccess = false;

        builder.AppendIndentation(3).Append(GeneratorConstants.InternalThings).Append("_Storage.UsedComponents = ");
        if (onUpdateEntity is null || onUpdateEntity.ParameterList.Parameters.Count == 0) {
            parameters = Array.Empty<(string, bool, bool, bool, bool)>();
            builder.AppendLine("System.Array.Empty<").Append(GeneratorConstants.InternalThings)
                .Append(".ComponentUsage>()");
        } else {
            parameters = new (string, bool, bool, bool, bool)[onUpdateEntity.ParameterList.Parameters.Count];
            int i = 0;

            bool empty = onUpdateEntity.ParameterList.Parameters.All(x => {
                string s = x.Type!.GetSymbol<INamedTypeSymbol>(compilation).ToDisplayString();
                return s == EntityFullName || s == SystemCommandsFullName;
            });

            if (empty) {
                builder.AppendLine("System.Array.Empty<").Append(GeneratorConstants.InternalThings)
                    .Append(".ComponentUsage>()");
            } else {
                builder.Append("new ").Append(GeneratorConstants.InternalThings).AppendLine(".ComponentUsage[] {");
            }

            foreach (ParameterSyntax parameter in onUpdateEntity.ParameterList.Parameters) {
                string name = parameter.Type!.GetSymbol<INamedTypeSymbol>(compilation).ToDisplayString();
                bool isRef = parameter.Modifiers.Any(x => x.IsKind(SyntaxKind.RefKeyword));
                bool isIn = parameter.Modifiers.Any(x => x.IsKind(SyntaxKind.InKeyword));
                bool isOut = parameter.Modifiers.Any(x => x.IsKind(SyntaxKind.OutKeyword));
                bool isAffective = parameter.Type!.GetSymbol<ITypeSymbol>(compilation).AllInterfaces.Any(
                    x => x.ToDisplayString() == AffectiveComponentFullName
                );

                if (name != EntityFullName && name != SystemCommandsFullName) {
                    builder.AppendIndentation(4).Append("new ").Append(GeneratorConstants.InternalThings)
                        .Append(".ComponentUsage(typeof(").Append(name).Append("), ").Append(isRef ? "true" : "false")
                        .AppendLine("),");
                    componentWriteAccess |= isRef;
                }

                parameters[i++] = (name, isRef, isIn, isOut, isAffective);
            }

            if (!empty) {
                builder.Remove(builder.Length - 1 - Environment.NewLine.Length, 1)
                    .AppendIndentation(3).Append('}');
            }
        }
        builder.AppendLine(";");

        builder.AppendIndentation(3).Append(GeneratorConstants.InternalThings)
            .Append("_Storage.ComponentWriteAccess = ").Append(componentWriteAccess ? "true" : "false")
            .AppendLine(";");

        builder.AppendIndentation(2).AppendLine("}").AppendLine();
        return parameters;
    }

    private void GenerateSystemExecutionMethod(
        StringBuilder builder, MethodDeclarationSyntax onUpdateEntity,
        (string parameterType, bool isRef, bool isIn, bool isOut, bool isAffective)[] parameters
    ) {
        builder.AppendIndentation(2)
            .Append("[System.Obsolete(\"").Append(InternalMethodObsoleteMessage).AppendLine("\")]")
            .AppendIndentation(2).AppendLine(
                "[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions." +
                "AggressiveOptimization)]"
            ).AppendIndentation(2).Append("protected override void ").Append(GeneratorConstants.InternalThings)
            .Append("_SystemExecution(").Append(GeneratorConstants.InternalThings).Append(".ExecutionData data, ")
            .Append(SystemCommandsFullName).AppendLine(" systemCommands) {");

        StringBuilder content = new StringBuilder(onUpdateEntity.ToFullString());
        content.IndexOf("void", out int index)
            .Replace("private", "", 0, index)
            .Replace("protected", "", 0, index)
            .Replace("internal", "", 0, index)
            .Replace("public", "", 0, index);

        builder.AppendLine("#if (!DEBUG)").AppendIndentation(3).AppendLine(
            "[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions." +
            "AggressiveInlining | System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]"
        ).Append(content).AppendLine("#endif").AppendLine();

        builder.AppendIndentation(3).Append(EntityFullName).AppendLine("? entity;");
        if (parameters.Any(x => x.isAffective)) {
            builder.AppendIndentation(3).AppendLine("bool changeArchetype;");
            builder.AppendIndentation(3).AppendLine("int archetypeHashCode;");
        }

        int i = 0;
        foreach ((string parameterType, bool isRef, _, _, _) in parameters) {
            if (parameterType == EntityFullName || parameterType == SystemCommandsFullName)
                continue;

            builder.AppendIndentation(3).Append("nint offset").Append(i).Append(" = data.GetOffset<")
                .Append(parameterType).AppendLine(">();");

            if (isRef)
                builder.AppendIndentation(3).Append(parameterType).Append(" oldParameter").Append(i).AppendLine(";");

            builder.AppendIndentation(3);
            if (isRef)
                builder.Append("ref ");
            builder.Append(parameterType).Append(" parameter").Append(i++);

            if (isRef) {
                builder.Append(" = ref ").Append(GeneratorConstants.InternalThings).Append(".NullRef<")
                    .Append(parameterType).Append(">()");
            }
            builder.AppendLine(";");
        }
        builder.AppendLine();

        builder.AppendIndentation(3)
            .AppendLine("for (nint i = data.StartIndex; i <= data.EndIndex; i += data.RecordSize) {")
            .AppendIndentation(4).AppendLine("entity = data.GetInternalComponent(i);")
            .AppendIndentation(4).AppendLine("if (entity is null)")
            .AppendIndentation(5).AppendLine("continue;").AppendLine();

        i = 0;
        foreach ((string parameterType, bool isRef, _, _, _) in parameters) {
            if (parameterType == EntityFullName || parameterType == SystemCommandsFullName)
                continue;

            builder.AppendIndentation(4).Append("parameter").Append(i).Append(" = ");
            if (isRef)
                builder.Append("ref ");
            builder.Append("data.Get<").Append(parameterType).Append(">(i + offset").Append(i++).AppendLine(");");

            if (isRef) {
                builder.AppendIndentation(4).Append("oldParameter").Append(i - 1).Append(" = parameter").Append(i - 1)
                    .AppendLine(";");
            }
        }
        builder.AppendLine();

        builder.AppendIndentation(4).Append("OnUpdateEntity(");
        i = 0;
        foreach ((string parameterType, bool isRef, _, _, _) in parameters) {
            if (parameterType == EntityFullName) {
                builder.Append("entity");
            } else if (parameterType == SystemCommandsFullName) {
                builder.Append("systemCommands");
            } else {
                if (isRef)
                    builder.Append("ref ");
                builder.Append("parameter").Append(i++);
            }
            builder.Append(", ");
        }

        if (parameters.Length != 0)
            builder.Remove(builder.Length - 2, 2);
        builder.AppendLine(");").AppendLine();

        if (parameters.Any(x => x.isAffective))
            builder.AppendIndentation(4).AppendLine("archetypeHashCode = 0;");

        i = 0;
        bool first = true;
        foreach ((string parameterType, bool isRef, _, _, bool isAffective) in parameters) {
            if (!isRef || !isAffective) {
                i++;
                continue;
            }

            builder.AppendIndentation(4).Append("changeArchetype ");
            if (first)
                first = false;
            else
                builder.Append('|');

            builder.Append(
                "= NoiseEngineInternal_DoNotUse.CompareAffectiveComponent(ref archetypeHashCode, in oldParameter"
            ).Append(i).Append(", in parameter").Append(i++).AppendLine(");");
        }

        // Update archetype.
        if (!first) {
            builder.AppendIndentation(4).AppendLine("if (changeArchetype) {");
            builder.AppendIndentation(5).AppendLine(
                "int hashCode = NoiseEngineInternal_DoNotUse.ArchetypeHashCode(entity);"
            );

            foreach ((string parameterType, bool isRef, _, _, bool isAffective) in parameters) {
                if (!isRef || !isAffective)
                    continue;

                builder.AppendIndentation(5)
                    .Append("hashCode ^= NoiseEngineInternal_DoNotUse.ArchetypeComponentHashCode<")
                    .Append(parameterType).AppendLine(">(entity);");
            }
            builder.AppendLine();

            builder.AppendIndentation(5).AppendLine("hashCode ^= archetypeHashCode;");
            builder.AppendIndentation(5).AppendLine(
                "NoiseEngineInternal_DoNotUse.ChangeArchetype(entity, hashCode, () => new (System.Type type, " +
                "int size, int affectiveHashCode)[] {"
            );

            i = 0;
            foreach ((string parameterType, bool isRef, _, _, bool isAffective) in parameters) {
                if (!isRef || !isAffective) {
                    i++;
                    continue;
                }

                builder.AppendIndentation(6).AppendLine("(").AppendIndentation(7).Append("typeof(").Append(parameterType)
                    .AppendLine("),").AppendIndentation(7).Append("System.Runtime.CompilerServices.Unsafe.SizeOf<")
                    .Append(parameterType).AppendLine(">(),").AppendIndentation(7)
                    .Append("IAffectiveComponent.GetAffectiveHashCode(data.Get<").Append(parameterType)
                    .Append(">(i + offset").Append(i++).AppendLine("))").AppendIndentation(6).AppendLine("),");
            }
            builder.Remove(builder.Length - 1 - Environment.NewLine.Length, 1);

            builder.AppendIndentation(5).AppendLine("});");

            builder.AppendIndentation(4).AppendLine("}");
        }

        builder.AppendIndentation(3).AppendLine("}");
        builder.AppendIndentation(2).AppendLine("}").AppendLine();
    }

}
