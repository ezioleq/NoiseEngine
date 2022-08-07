﻿using NoiseEngine.Nesl.CompilerTools.Architectures.SpirV.Types;
using NoiseEngine.Nesl.Emit.Attributes;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NoiseEngine.Nesl.CompilerTools.Architectures.SpirV;

internal class SpirVCompiler {

    private readonly ConcurrentDictionary<NeslType, Lazy<SpirVType>> types =
        new ConcurrentDictionary<NeslType, Lazy<SpirVType>>();
    private readonly ConcurrentDictionary<NeslMethod, Lazy<SpirVFunction>> functions =
        new ConcurrentDictionary<NeslMethod, Lazy<SpirVFunction>>();

    private uint nextId;

    public NeslAssembly NeslAssembly { get; }

    public SpirVBuiltInTypes BuiltInTypes { get; }

    public SpirVGenerator Header { get; }
    public SpirVGenerator TypesAndVariables { get; }

    public SpirVCompiler(NeslAssembly neslAssembly) {
        NeslAssembly = neslAssembly;

        BuiltInTypes = new SpirVBuiltInTypes(this);

        Header = new SpirVGenerator(this);
        TypesAndVariables = new SpirVGenerator(this);
    }

    public byte[] Compile() {
        Header.Emit(SpirVOpCode.OpCapability, (uint)Capability.Shader);
        Header.Emit(SpirVOpCode.OpMemoryModel, (uint)AddressingModel.Logical, (uint)MemoryModel.Glsl450);

        Parallel.ForEach(NeslAssembly.Types.SelectMany(x => x.Methods), x => GetSpirVFunction(x));

        SpirVGenerator generator = new SpirVGenerator(this);
        FirstWords(generator);

        generator.Writer.WriteBytes(Header.Writer.AsSpan());
        generator.Writer.WriteBytes(TypesAndVariables.Writer.AsSpan());

        foreach (Lazy<SpirVFunction> lazy in functions.Values)
            lazy.Value.Construct(generator);

        // Set bound.
        BinaryPrimitives.WriteUInt32BigEndian(generator.Writer.AsSpan(12), GetNextId().RawId);

        return generator.Writer.AsSpan().ToArray();
    }

    internal SpirVId GetNextId() {
        return new SpirVId(Interlocked.Increment(ref nextId));
    }

    internal SpirVType GetSpirVType(NeslType? neslType) {
        if (neslType is null)
            return BuiltInTypes.GetOpTypeVoid();

        return types.GetOrAdd(neslType, _ => new Lazy<SpirVType>(() => {
            if (neslType.Attributes.TryCastAnyAttribute(
                out PlatformDependentTypeRepresentationAttribute? platformDependentTypeRepresentation
            )) {
                string? name = platformDependentTypeRepresentation.SpirVTargetName;
                if (name is null)
                    throw new InvalidOperationException();

                if (!BuiltInTypes.TryGetTypeByName(name, out SpirVType? type))
                    throw new InvalidOperationException();
                return type;
            }

            return new SpirVType(this, neslType);
        })).Value;
    }

    internal SpirVFunction GetSpirVFunction(NeslMethod neslMethod) {
        return functions.GetOrAdd(neslMethod,
            _ => new Lazy<SpirVFunction>(() => new SpirVFunction(this, neslMethod))).Value;
    }

    /// <summary>
    /// https://registry.khronos.org/SPIR-V/specs/1.2/SPIRV.html#_a_id_physicallayout_a_physical_layout_of_a_spir_v_module_and_instruction
    /// </summary>
    private void FirstWords(SpirVGenerator generator) {
        generator.Writer.WriteUInt32(0x07230203); // Magic number.
        generator.Writer.WriteUInt32(0x00010200); // Version number.
        generator.Writer.WriteUInt32(0x1ec5712d); // Generator's magic number.
        generator.Writer.WriteUInt32(0x0); // Bound soace.
        generator.Writer.WriteUInt32(0x0);
    }

}
