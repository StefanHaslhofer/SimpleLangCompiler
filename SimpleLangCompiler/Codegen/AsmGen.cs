using SimpleLangCompiler.FrontEnd;
using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.Codegen;

public class AsmGen(RegisterAllocator regAlloc, string buildEnv)
{
    // Holds the starting lines of the .bss segment.
    public readonly List<string> BssSegment = [];

    // Holds all global integer declarations.
    public readonly List<string> BssIntSegment = [];

    // Holds all global char declarations.
    public readonly List<string> BssCharSegment = [];

    // Start of the text segment.
    public readonly List<string> TextSegmentPrologue = [];

    // Maps a function name to its list of assembler instructions.
    // Each entry in the list represents one instruction.
    public readonly Dictionary<string, List<string>> Functions = [];

    // End of the text segment (calls main).
    public readonly List<string> TextSegmentEpilogue = [];

    public string GetNewLabel() => $"L{_labelCount++}";

    private const int DWordSize = 8;
    private int _labelCount;

    // TODO this method should write to a file or at least should produce output that can be automatically linked
    public void Print()
    {
        foreach (var s in BssSegment)
        {
            Console.WriteLine(s);
        }

        foreach (var s in BssIntSegment)
        {
            Console.WriteLine(s);
        }

        foreach (var s in BssCharSegment)
        {
            Console.WriteLine(s);
        }

        foreach (var s in TextSegmentPrologue)
        {
            Console.WriteLine(s);
        }

        foreach (var f in Functions)
        {
            foreach (var s in f.Value)
            {
                Console.WriteLine(s);
            }
        }

        foreach (var s in TextSegmentEpilogue)
        {
            Console.WriteLine(s);
        }
    }

    // Emit assembler code that transforms an integer operand into a character and vice versa.
    public void GenOrdChrFunc(Operand x, Obj func)
    {
        var asm = Functions[func.Name];
        Load(x, asm, true);
        // The ORD/CHR function is implemented as a single assembly instruction,
        // so we skip the usual function call convention and directly store its result in register a0
        // for consistent handling outside this method.
        asm.Add($"\tandi {Register.A0.ToLabel()}, {x.Reg!.Value.ToLabel()}, 0xff"); // mask first byte
        regAlloc.Free(x.Reg.Value);
    }

    // Generate assembler code for built-in "put" function.
    public void GenPutFunc(Obj func)
    {
        // the prologue already saves the char param on the stack
        var stackFrameSize = GenFuncPrologue(func);
        var asm = Functions[func.Name];

        // print depends on environment
        switch (buildEnv)
        {
            case "sim":
                // syscall 11 is "print character"
                asm.Add("\tli a7, 11");
                asm.Add("\tecall");
                break;
            case "linux":
                // Linux uses write to file for printing. Because register a0 is used for file descriptor,
                // we need to push the char param onto the stack first,
                // which is already done in the prologue.
                asm.Add("\tli a7, 64");
                asm.Add("\tli a0, 1"); // a0 = 1 (file descriptor for stdout)
                asm.Add($"\taddi a1, fp, {stackFrameSize - 16 - DWordSize}"); // start address of output
                asm.Add("\tli a2, 1"); // number of bytes to write (one char = 1 byte)
                asm.Add("\tecall");
                break;
            default:
                throw new NotImplementedException($"Environment '{buildEnv}' not supported for 'put' function");
        }

        GenFuncEpilogue(func);
    }

    // Generate assembler code for built-in "putLn" function.
    public void GenPutLnFunc(Obj func)
    {
        int stackFrameSize = GenFuncPrologue(func);

        var asm = Functions[func.Name];

        // print depends on environment (see GenPutFunc for further info)
        switch (buildEnv)
        {
            case "sim":
                asm.Add("\tli a0, 10"); // newline character
                asm.Add("\tli a7, 11");
                asm.Add("\tecall");
                break;
            case "linux":
                // store newline character on stack
                var charPos = stackFrameSize - 16 - DWordSize;
                asm.Add("\tli t0, 10"); // newline character
                asm.Add($"\tsb t0, {charPos}(fp)");

                // execute Linux syscall
                asm.Add("\tli a7, 64");
                asm.Add("\tli a0, 1");
                asm.Add($"\taddi a1, fp, {charPos}");
                asm.Add("\tli a2, 1");
                asm.Add("\tecall");
                break;
            default:
                throw new NotImplementedException($"Environment '{buildEnv}' not supported for 'put' function");
        }

        GenFuncEpilogue(func);
    }

    public Operand VarOperand(Obj o)
    {
        Operand x = new Operand(o.Type, OperandKind.Var);
        if (o.Level == 0) // global variable
        {
            x.AddrMode = AddressingMode.Abs;
            x.AdrOffset = o.AdrOffset;
            x.Label = o.Name;
        }
        else // function parameter and local variables
        {
            x.AddrMode = AddressingMode.RegRel;
            x.AdrOffset = o.AdrOffset;
            x.Reg = Register.FP;
        }

        return x;
    }

    public Operand ValOperand(Struct type, int val)
    {
        Operand x = new Operand(type, OperandKind.Val);
        x.Val = val;

        return x;
    }

    public Operand FuncOperand(Obj o)
    {
        Operand x = new Operand(o.Type, OperandKind.Func);
        x.AdrOffset = o.AdrOffset;
        x.Label = o.Name;

        return x;
    }

    // Write parameters in registers a0...a7 and call function.
    public void GenFuncCall(Obj func, Operand target, List<Operand> args, bool isFactor = false)
    {
        var asm = Functions[func.Name];

        // handle special cases for ORD and CHR functions
        if (target.Label == "ORD" || target.Label == "CHR")
        {
            GenOrdChrFunc(args[0], func);
        }
        else
        {
            foreach (var arg in args)
            {
                Load(arg, asm, true);
            }

            asm.Add($"\tcall {target.Label}");
        }

        // free all param registers after function return
        regAlloc.FreeAllParams();

        if (isFactor)
        {
            // store return value in tmp register
            var reg = regAlloc.Alloc();
            asm.Add($"\tmv {reg.ToLabel()}, {Register.A0.ToLabel()}");
            target.Reg = reg;
            target.AddrMode = AddressingMode.Reg;
        }
    }

    // Generate assembler code for conditions.
    public void GenJcc(TokenKind op, Operand x, Operand y, bool fjump, string targetLbl, Obj func)
    {
        var asm = Functions[func.Name];

        // jump directly if both operands are fixed values and the condition is true (no register allocation needed)
        if (x.Kind == OperandKind.Val && y.Kind == OperandKind.Val)
        {
            bool res = op switch
            {
                TokenKind.Assign => x.Val == y.Val,
                TokenKind.NotEq => x.Val != y.Val,
                TokenKind.Less => x.Val < y.Val,
                TokenKind.GreaterEq => x.Val >= y.Val,
                TokenKind.Greater => x.Val > y.Val,
                TokenKind.LessEq => x.Val <= y.Val,
                _ => throw new FatalError($"Unsupported comparison operation: {op}")
            };

            // xor to invert condition on false jumps
            if (fjump) res = !res;

            // jump if condition is true
            if (res)
            {
                asm.Add($"\tj {targetLbl}");
            }

            return;
        }

        // always load both operands into registers to keep code simple
        Load(x, asm);
        Load(y, asm);

        int opCode = op switch
        {
            TokenKind.Assign => 0,
            TokenKind.NotEq => 1,
            TokenKind.Less => 2,
            TokenKind.GreaterEq => 3,
            TokenKind.Greater => 4,
            TokenKind.LessEq => 5,
            _ => throw new FatalError($"Unsupported comparison operation: {op}")
        };

        // xor to invert condition on false jumps
        if (fjump) opCode ^= 1;

        // convert opcodes to assembler instructions
        string instr = opCode switch
        {
            0 => "beq", // ==
            1 => "bne", // != ('#' in my case -> grammar on GitHub differs from original) 
            2 => "blt", // <
            3 => "bge", // >=
            4 => "bgt", // >
            5 => "ble", // <=
            _ => throw new FatalError("Invalid opcode")
        };

        asm.Add($"\t{instr} {x.Reg!.Value.ToLabel()}, {y.Reg!.Value.ToLabel()}, {targetLbl}");
        regAlloc.Free(x.Reg.Value);
        regAlloc.Free(y.Reg.Value);
    }

    // Generate assembler code for assignments.
    public void GenAssign(Operand x, Operand y, Obj func)
    {
        var asm = Functions[func.Name];
        var storeInstr = x.Struct.Type == StructKind.Int ? "sd" : "sb";
        var offsetX = GetOperandOffset(x);

        // always load value of y into register
        Load(y, asm);

        if (x.AddrMode == AddressingMode.RegRel)
        {
            // x := local var
            asm.Add($"\t{storeInstr} {y.Reg!.Value.ToLabel()}, {offsetX}(fp)");
        }
        else if (x.AddrMode == AddressingMode.Abs)
        {
            // x := global var
            var rd = regAlloc.Alloc();
            asm.Add($"\tla {rd.ToLabel()}, {x.Label}");
            asm.Add($"\t{storeInstr} {y.Reg!.Value.ToLabel()}, 0({rd.ToLabel()})");
            regAlloc.Free(rd);
        }

        // free after assignment
        regAlloc.Free(y.Reg!.Value);
    }

    // Generate assembler code for arithmetic operations.
    public void GenArithmetic(TokenKind op, Operand x, Operand y, Obj? func)
    {
        // directly calculate result if both operands are fixed values
        if (x.Kind == OperandKind.Val && y.Kind == OperandKind.Val)
        {
            switch (op)
            {
                case TokenKind.Plus:
                    x.Val += y.Val; break;
                case TokenKind.Minus:
                    x.Val -= y.Val; break;
                case TokenKind.Star:
                    x.Val *= y.Val; break;
                case TokenKind.Slash:
                    x.Val /= y.Val; break;
                case TokenKind.Percent:
                    x.Val %= y.Val; break;
            }

            return;
        }

        var asm = Functions[func!.Name];
        Load(x, asm);
        Load(y, asm);

        // note: register allocation is not optimal
        var rd = regAlloc.Alloc();
        switch (op)
        {
            case TokenKind.Plus:
                asm.Add($"\tadd {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {y.Reg!.Value.ToLabel()}");
                break;
            case TokenKind.Minus:
                asm.Add($"\tsub {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {y.Reg!.Value.ToLabel()}");
                break;
            case TokenKind.Star:
                asm.Add($"\tmul {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {y.Reg!.Value.ToLabel()}");
                break;
            case TokenKind.Slash:
                asm.Add($"\tdiv {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {y.Reg!.Value.ToLabel()}");
                break;
            case TokenKind.Percent:
                asm.Add($"\trem {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {y.Reg!.Value.ToLabel()}");
                break;
        }

        regAlloc.Free(x.Reg!.Value);
        regAlloc.Free(y.Reg!.Value);
        x.Reg = rd;
    }

    // Add a jump instruction to a label.
    public void GenJump(string lbl, Obj func)
    {
        var asm = Functions[func.Name];
        asm.Add($"\tj {lbl}");
    }

    // Add a label to the assembly code.
    public void GenLbl(string lbl, Obj func)
    {
        var asm = Functions[func.Name];
        asm.Add($"{lbl}:");
    }

    // Load operand value into register.
    private void Load(Operand x, List<string> asm, bool isParam = false)
    {
        var rd = regAlloc.Alloc(isParam);

        if (x.Kind == OperandKind.Val && x.AddrMode == null)
        {
            // load the immediate value into the register for simplicity
            asm.Add($"\tli {rd.ToLabel()}, {x.Val}");
        }
        else
        {
            var loadInstr = GetLoadInstr(x.Struct);
            switch (x.AddrMode)
            {
                case AddressingMode.Abs:
                    // load symbol address into rd
                    asm.Add($"\tla {rd.ToLabel()}, {x.Label}");
                    // load word from address referenced in rd to get value
                    asm.Add($"\t{loadInstr} {rd.ToLabel()}, 0({rd.ToLabel()})");
                    break;
                case AddressingMode.RegRel:
                    asm.Add($"\t{loadInstr} {rd.ToLabel()}, {GetOperandOffset(x)}(fp)");
                    break;
                case AddressingMode.Reg:
                    // free unnecessarily allocated register if operand is already in register
                    regAlloc.Free(rd);
                    return;
            }
        }

        x.Reg = rd;
        x.AddrMode = AddressingMode.Reg;
        x.AdrOffset = 0;
    }

    public int GenFuncPrologue(Obj obj)
    {
        // initialize function area
        Functions.TryAdd(obj.Name, [$"{obj.Name}:"]);
        var funcAsm = Functions[obj.Name];

        var stackFrameSize = CalculateStackFrameSize(obj.Locals.Count);
        // allocate memory for all locals
        funcAsm.Add($"\taddi sp, sp, -{stackFrameSize}");
        // save return address
        funcAsm.Add($"\tsd ra, {stackFrameSize - 8}(sp)");
        // save caller frame pointer
        funcAsm.Add($"\tsd fp, {stackFrameSize - 16}(sp)");

        // Push function parameters onto stack (only first n locals are params stored in registers a0 to a7).
        // This is not optimal because registers a0 to a7 could be used directly,
        // but I want to keep it as simple as possible.
        foreach (var (i, param) in obj.Locals.Take(obj.NPars).Index())
        {
            // store double word or byte depending on parameter type
            var storeInstr = param.Type.Type == StructKind.Int ? "sd" : "sb";
            funcAsm.Add($"\t{storeInstr} a{i}, {stackFrameSize - 16 - (i + 1) * DWordSize}(sp)");
        }

        // set frame pointer (new fp = old sp)
        funcAsm.Add($"\taddi fp, sp, {stackFrameSize}");
        return stackFrameSize;
    }

    public void GenFuncEpilogue(Obj obj)
    {
        var funcAsm = Functions[obj.Name];

        // label so early returns can jump here
        funcAsm.Add($"{obj.Name}_ret:");
        // Calculate space for locals
        var stackFrameSize = CalculateStackFrameSize(obj.Locals.Count);
        // restore caller frame pointer
        funcAsm.Add($"\tld fp, {stackFrameSize - 16}(sp)");
        // restore return address
        funcAsm.Add($"\tld ra, {stackFrameSize - 8}(sp)");
        // deallocate stack frame
        funcAsm.Add($"\taddi sp, sp, {stackFrameSize}");
        funcAsm.Add("\tret");
    }

    public void GenTextPrologue()
    {
        TextSegmentPrologue.Add(".text");
        TextSegmentPrologue.Add("j skip");
    }

    public void GenTextEpilogue()
    {
        // call main
        TextSegmentEpilogue.Add("skip:");
        TextSegmentEpilogue.Add("\tcall main");
    }

    // Generate assembler code for variable declaration.
    public void GenVarDecl(Obj obj)
    {
        if (obj.Level == 0) // global variable
        {
            if (BssSegment.Count == 0)
            {
                BssSegment.Add(".bss");
                BssSegment.Add(".align 3");
            }

            // 64-bit integers must be 8-byte aligned (.align 3 = 2^3 = 8).
            // Mixing chars between them would waste 7 padding bytes per char,
            // so ints and chars are grouped in separate blocks.
            if (obj.Type.Type == StructKind.Int)
            {
                var asmStr = $"{obj.Name}: .space 8";
                BssIntSegment.Add(asmStr);
            }
            else if (obj.Type.Type == StructKind.Char)
            {
                var asmStr = $"{obj.Name}: .space 1";
                BssCharSegment.Add(asmStr);
            }
        }
    }

    public void GenReturn(Operand x, Obj func)
    {
        var asm = Functions[func.Name];

        switch (x.Kind)
        {
            case OperandKind.Var:
            case OperandKind.Func:
                if (x.AddrMode == AddressingMode.Reg)
                {
                    asm.Add($"\tmv a0, {x.Reg!.Value.ToLabel()}");
                    regAlloc.Free(x.Reg!.Value);
                }
                else
                {
                    // register relative vars have to be loaded into register first
                    var rd = regAlloc.Alloc();
                    var loadInstr = GetLoadInstr(x.Struct);
                    asm.Add($"\t{loadInstr} {rd.ToLabel()}, {GetOperandOffset(x)}(fp)");
                    asm.Add($"\tmv a0, {rd.ToLabel()}");
                    regAlloc.Free(rd);
                }

                break;
            case OperandKind.Val:
                asm.Add($"\tli a0, {x.Val}");
                break;
        }

        asm.Add($"\tj {func.Name}_ret");
    }
    
    // Negate operand.
    public void GenNeg(Operand x, Obj func)
    {
        // directly negate if operand is a fixed value
        if (x.Kind == OperandKind.Val)
        {
            x.Val = -x.Val;
            return;
        }
        
        var asm = Functions[func.Name];
        Load(x, asm);
        asm.Add($"\tneg {x.Reg!.Value.ToLabel()}, {x.Reg!.Value.ToLabel()}");
    }

    // Stack frame size = (num_locals × 8) + 8, aligned to 16 bytes.
    // Note: all locals treated as 8 bytes wide for simplicity.
    private int CalculateStackFrameSize(int numOfLocals)
    {
        var space = numOfLocals * DWordSize + 16;
        // allocate stack frame (needs to be 16 byte aligned according to ABI spec)
        return (int)(Math.Ceiling(space / 16.0) * 16);
    }

    private int GetOperandOffset(Operand x)
    {
        return -16 - DWordSize * ((x.AdrOffset ?? 0) + 1);
    }

    private string GetLoadInstr(Struct type)
    {
        return type.Type == StructKind.Int ? "ld" : "lb";
    }
}