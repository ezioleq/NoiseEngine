﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NoiseEngine.Generator;
using System.Text;

namespace NoiseEngine.InternalGenerator.Jobs {
    [Generator]
    public class EntityQuerySourceGenerator : ISourceGenerator {

        public void Execute(GeneratorExecutionContext context) {
            StringBuilder builder = new StringBuilder();
            for (int i = 1; i <= JobsGeneratorHelper.ArgumentsCount; i++) {
                builder.AppendLine("// <auto-generated />").AppendLine();
                builder.AppendLine("using System;").AppendLine("using System.Collections;")
                    .AppendLine("using System.Collections.Generic;")
                    .AppendLine("using System.Runtime.CompilerServices;");
                builder.AppendLine().AppendLine("namespace NoiseEngine.Jobs;").AppendLine();

                builder.Append("public class EntityQuery<");
                for (int j = 1; j <= i; j++)
                    builder.Append('T').Append(j).Append(", ");
                builder.Remove(builder.Length - 2, 2);
                builder.Append("> : EntityQuery, IEnumerable<(Entity entity");

                for (int j = 1; j <= i; j++)
                    builder.Append(", T").Append(j).Append(" component").Append((char)(j + 64));
                builder.AppendLine(")>");

                for (int j = 1; j <= i; j++)
                    builder.AppendIndentation().Append("where T").Append(j).AppendLine(" : IComponent");
                builder.AppendLine("{");

                builder.AppendLine();
                builder.AppendIndentation().AppendLine(
                    "internal override Type[] UsedComponentsInternal { get; } = new Type[] {"
                );
                builder.AppendIndentation(2);
                for (int j = 1; j <= i; j++)
                    builder.Append("typeof(T").Append(j).Append("), ");
                builder.Remove(builder.Length - 2, 2);
                builder.AppendLine().AppendIndentation().AppendLine("};").AppendLine();

                builder.AppendIndentation().AppendLine("internal EntityQuery(EntityWorld world) : base(world) {");
                builder.AppendIndentation().AppendLine("}").AppendLine();

                // IEnumerator.
                builder.AppendLine(@"   /// <summary>
    /// Returns an enumerator that iterates through this <see cref=""EntityQuery""/>.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through this <see cref=""EntityQuery""/>.</returns>");
                builder.AppendIndentation().Append("public IEnumerator<(Entity entity");
                for (int j = 1; j <= i; j++)
                    builder.Append(", T").Append(j).Append(" component").Append((char)(j + 64));
                builder.AppendLine(")> GetEnumerator() {");

                builder.AppendIndentation(2).Append("(Entity");
                for (int j = 1; j <= i; j++)
                    builder.Append(", T").Append(j);
                builder.AppendLine(") v = default;");
                builder.AppendLine();

                builder.AppendIndentation(2).AppendLine("foreach (Archetype archetype in archetypes) {");

                for (int j = 1; j <= i; j++) {
                    builder.AppendIndentation(3).Append("nint offset").Append(j)
                        .Append(" = archetype.Offsets[typeof(T").Append(j).AppendLine(")];");
                }
                builder.AppendLine();

                builder.AppendIndentation(3).AppendLine("foreach (ArchetypeChunk chunk in archetype.chunks) {");
                builder.AppendIndentation(4).AppendLine("nint max = chunk.Count * chunk.RecordSize;");
                builder.AppendIndentation(4).AppendLine("for (nint i = 0; i < max; i += chunk.RecordSize) {");

                builder.AppendIndentation(5).AppendLine("Span<byte> data = chunk.StorageDataSpan;");
                builder.AppendIndentation(5).AppendLine("do {");
                builder.AppendIndentation(6)
                    .AppendLine("v.Item1 = Unsafe.ReadUnaligned<EntityInternalComponent>(ref data[(int)i]).Entity!;");
                builder.AppendIndentation(6).AppendLine("if (v.Item1 is null)");
                builder.AppendIndentation(7).AppendLine("goto Skip;").AppendLine();

                for (int j = 1; j <= i; j++) {
                    builder.AppendIndentation(6).Append("v.Item").Append(j + 1).Append(" = Unsafe.ReadUnaligned<T")
                        .Append(j).Append(">(ref data[(int)(i + offset").Append(j).AppendLine(")]);");
                }

                builder.AppendIndentation(5).AppendLine("} while (v.Item1.chunk != chunk);").AppendLine();

                builder.AppendIndentation(5).AppendLine("yield return v;");
                builder.AppendIndentation(5).AppendLine("Skip:");
                builder.AppendIndentation(6).AppendLine("continue;");

                builder.AppendIndentation(4).AppendLine("}");
                builder.AppendIndentation(3).AppendLine("}");
                builder.AppendIndentation(2).AppendLine("}");

                builder.AppendIndentation().AppendLine("}").AppendLine();

                // IEnumerable not generic.
                builder.AppendLine(@"    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }").AppendLine();

                builder.AppendLine("}");

                context.AddSource($"EntityQueryT{i}.generated.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
                builder.Clear();
            }
        }

        public void Initialize(GeneratorInitializationContext context) {
        }

    }
}
