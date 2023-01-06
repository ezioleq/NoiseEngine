﻿using NoiseEngine.Nesl.Emit;
using System;
using System.Collections.Generic;

namespace NoiseEngine.Nesl.CompilerTools.Architectures.SpirV.IlCompilation;

internal class IlCompiler {

    private readonly IEnumerable<Instruction> instructions;

    public SpirVCompiler Compiler { get; }
    public NeslMethod NeslMethod { get; }
    public SpirVGenerator Generator { get; }
    public IReadOnlyList<SpirVVariable> Parameters { get; }

    public ArithmeticOperations ArithmeticOperations { get; }
    public BranchOperations BranchOperations { get; }
    public DefOperations DefOperations { get; }
    public LoadOperations LoadOperations { get; }
    public LoadElementOperations LoadElementOperations { get; }
    public LoadFieldOperations LoadFieldOperations { get; }

    public IlCompiler(
        SpirVCompiler compiler, IEnumerable<Instruction> instructions, NeslMethod neslMethod, SpirVGenerator generator,
        IReadOnlyList<SpirVVariable> parameters
    ) {
        Compiler = compiler;
        this.instructions = instructions;
        NeslMethod = neslMethod;
        Generator = generator;
        Parameters = parameters;

        ArithmeticOperations = new ArithmeticOperations(this);
        BranchOperations = new BranchOperations(this);
        DefOperations = new DefOperations(this);
        LoadOperations = new LoadOperations(this);
        LoadElementOperations = new LoadElementOperations(this);
        LoadFieldOperations = new LoadFieldOperations(this);
    }

    public void Compile() {
        foreach (Instruction instruction in instructions) {
            switch (instruction.OpCode) {
                #region ArithmeticOperations

                case OpCode.Negate:
                    ArithmeticOperations.Negate(instruction);
                    break;
                case OpCode.Add:
                    ArithmeticOperations.Add(instruction);
                    break;
                case OpCode.Subtract:
                    ArithmeticOperations.Subtract(instruction);
                    break;
                case OpCode.Multiple:
                    ArithmeticOperations.Multiple(instruction);
                    break;
                case OpCode.Divide:
                    ArithmeticOperations.Divide(instruction);
                    break;
                case OpCode.Modulo:
                    ArithmeticOperations.Modulo(instruction);
                    break;
                case OpCode.Remainder:
                    ArithmeticOperations.Remainder(instruction);
                    break;

                #endregion
                #region BranchOperations

                case OpCode.Call:
                    BranchOperations.Call(instruction);
                    break;
                case OpCode.Return:
                    BranchOperations.Return();
                    break;
                case OpCode.ReturnValue:
                    BranchOperations.ReturnValue(instruction);
                    break;

                #endregion
                #region DefOperations

                case OpCode.DefVariable:
                    DefOperations.DefVariable(instruction);
                    break;

                #endregion
                #region LoadOperations

                case OpCode.Load:
                    LoadOperations.Load(instruction);
                    break;
                case OpCode.LoadUInt32:
                    LoadOperations.LoadUInt32(instruction);
                    break;
                case OpCode.LoadFloat32:
                    LoadOperations.LoadFloat32(instruction);
                    break;

                #endregion
                #region LoadElementOperations

                case OpCode.LoadElement:
                    LoadElementOperations.LoadElement(instruction);
                    break;
                case OpCode.SetElement:
                    LoadElementOperations.SetElement(instruction);
                    break;

                #endregion
                #region LoadFieldOperations

                case OpCode.LoadField:
                    LoadFieldOperations.LoadField(instruction);
                    break;
                case OpCode.SetField:
                    LoadFieldOperations.SetField(instruction);
                    break;

                #endregion
                default:
                    throw new NotImplementedException();
            }
        }
    }

}