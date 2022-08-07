﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NoiseEngine.Nesl.CompilerTools.Architectures.SpirV;

internal class SpirVBuiltInTypes {

    private readonly ConcurrentDictionary<ComparableArray, Lazy<SpirVType>> types =
        new ConcurrentDictionary<ComparableArray, Lazy<SpirVType>>();

    public SpirVCompiler Compiler { get; }

    public SpirVBuiltInTypes(SpirVCompiler compiler) {
        Compiler = compiler;
    }

    public bool TryGetTypeByName(string name, [NotNullWhen(true)] out SpirVType? type) {
        switch (name) {
            case nameof(SpirVOpCode.OpTypeVoid):
                type = GetOpTypeVoid();
                return true;
            default:
                type = null;
                return false;
        }
    }

    public SpirVType GetOpTypeVoid() {
        return types.GetOrAdd(new ComparableArray(new object[] { SpirVOpCode.OpTypeVoid }),
            _ => new Lazy<SpirVType>(() => {
                lock (Compiler.TypesAndVariables) {
                    SpirVId id = Compiler.GetNextId();
                    Compiler.TypesAndVariables.Emit(SpirVOpCode.OpTypeVoid, id);
                    return new SpirVType(Compiler, id);
                }
        })).Value;
    }

    public SpirVType GetOpTypeFunction(SpirVType returnType, params SpirVType[] parameters) {
        List<object> objects = new List<object> {
            SpirVOpCode.OpTypeFunction, returnType
        };

        foreach (SpirVType type in parameters)
            throw new NotImplementedException(); //objects.Add(id);

        return types.GetOrAdd(new ComparableArray(objects.ToArray()), _ => new Lazy<SpirVType>(() => {
            lock (Compiler.TypesAndVariables) {
                SpirVId id = Compiler.GetNextId();
                Compiler.TypesAndVariables.Emit(SpirVOpCode.OpTypeFunction, id, returnType.Id);
                return new SpirVType(Compiler, id);
            }
        })).Value;
    }

}
