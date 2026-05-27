using System.Reflection.Emit;
using SimpleLangCompiler.FrontEnd;
using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.Codegen;

public class AsmGen(RegisterAllocator regAlloc)
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
    
    private const int DWordSize = 8;

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

        return x;
    }
    
    // Generate assembler code for assignments
    public void GenAssign(Operand x, Operand y, Obj func)
    {
        var asm = Functions[func.Name];
        var storeInstr = x.Struct.Type == StructKind.Int ? "sd" : "sb";
        var offsetX = GetOperandOffset(x);

        // always load value of y into register
        Load(y, asm);

        // TODO check and comment
        if (x.AddrMode == AddressingMode.RegRel)
        {
            asm.Add($"{storeInstr} {y.Reg!.Value.ToLabel()}, {offsetX}(fp)");
        }
        else if (x.AddrMode == AddressingMode.Abs)
        {
            var rd = regAlloc.Alloc();
            asm.Add($"la {rd}, {x.Label}");
            asm.Add($"{storeInstr} {y.Reg!.Value.ToLabel()}, 0({rd})");
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
            }
        }
        else
        {
            var asm = Functions[func!.Name];
            Load(x, asm);
            Load(y, asm);

            // note: register allocation is not optimal
            var rd = regAlloc.Alloc();
            switch (op)
            {
                case TokenKind.Plus:
                    asm.Add($"\tadd {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {x.Reg!.Value.ToLabel()}");
                    break;
                case TokenKind.Minus:
                    asm.Add($"\tsub {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {x.Reg!.Value.ToLabel()}");
                    break;
                case TokenKind.Star:
                    asm.Add($"\tmul {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {x.Reg!.Value.ToLabel()}");
                    break;
                case TokenKind.Slash:
                    asm.Add($"\tdiv {rd.ToLabel()}, {x.Reg!.Value.ToLabel()}, {x.Reg!.Value.ToLabel()}");
                    break;
            }

            regAlloc.Free(x.Reg!.Value);
            regAlloc.Free(y.Reg!.Value);
            x.Reg = rd;
        }
    }

    // Load operand value into register.
    private void Load(Operand x, List<string> asm)
    {
        var rd = regAlloc.Alloc();

        if (x.Kind == OperandKind.Val)
        {
            // load the immediate value into the register for simplicity
            asm.Add($"\tli {rd.ToLabel()}, {x.Val}");
        }
        else
        {
            var loadInstr = x.Struct.Type == StructKind.Int ? "ld" : "lb";
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
                    // do nothing if operand is already in register
                    return;
            }
        }

        x.Reg = rd;
        x.AddrMode = AddressingMode.Reg;
        x.AdrOffset = 0;
    }

    public void GenFuncPrologue(Obj obj)
    {
        // initialize function area
        Functions.Add(obj.Name, [$"{obj.Name}:"]);
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
            // TODO free register a{i-1} --> maybe not here
        }

        // set frame pointer (new fp = old sp)
        funcAsm.Add($"\taddi fp, sp, {stackFrameSize}");
    }

    public void GenFuncEpilogue(Obj obj)
    {
        var funcAsm = Functions[obj.Name];
        
        // label so early returns can jump here
        funcAsm.Add($"{obj.Name}_ret:");
        // Calculate space for locals
        var stackFrameSize = CalculateStackFrameSize(obj.Locals.Count);
        // restore return address
        funcAsm.Add($"\tlw ra, {stackFrameSize - 4}(sp)");
        // restore caller frame pointer
        funcAsm.Add($"\tlw fp, {stackFrameSize - 8}(sp)");
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

        if (x.Kind != OperandKind.None)
        {
            // move result into return register
            asm.Add($"\tmv a0, {x.Reg!.Value.ToLabel()}");
            regAlloc.Free(x.Reg!.Value);    
        }
        
        asm.Add($"\tj {func.Name}_ret");
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
        return -16 - DWordSize * (x.AdrOffset ?? 0 + 1);
    }
}