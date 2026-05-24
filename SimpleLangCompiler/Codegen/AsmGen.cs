using System.Reflection.Emit;
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
    
    // Holds executable code.
    public readonly List<string> TextSegment = [];

    // Maps a function name to its list of assembler instructions.
    // Each entry in the list represents one instruction.
    public readonly Dictionary<string, List<string>> Functions = [];

    private readonly RegisterAllocator _regAlloc = regAlloc;

    public void Print()
    {
        // TODO this method should write to a file or at least should produce output that can be automatically linked
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

        foreach (var f in Functions)
        {
            foreach (var s in f.Value)
            {
                Console.WriteLine(s);
            }
        }
    }

    public Operand VarOperand(Obj o)
    {
        Operand x = new Operand(o.Type, OperandKind.Var);
        if (o.Level == 0)
        {
            // global variable
            x.AddrMode = AddressingMode.Abs;
            x.AdrOffset = o.AdrOffset;
            x.Label = o.Name;
        }
        else
        {
            // local variable
            // TODO how to address params and locals? Both are in the same local scope,
            //  maybe all should be on the stack and adressed via RegRel
            x.AddrMode = AddressingMode.Reg;
            x.AdrOffset = o.AdrOffset;
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


        return x;
    }

    public void GenFuncPrologue(Obj obj)
    {
        // initialize function area
        Functions.Add(obj.Name, [$"{obj.Name}:"]);
        var funcAsm = Functions[obj.Name];

        var stackFrameSize = CalculateStackFrameSize(obj.Locals.Count);
        funcAsm.Add($"\taddi sp, sp, -{stackFrameSize}");
        // save return address
        funcAsm.Add($"\tsw ra, {stackFrameSize - 4}(sp)");
        // save caller frame pointer
        funcAsm.Add($"\tsw fp, {stackFrameSize - 8}(sp)");
        
        // push function parameters onto stack (only first n locals are params stored in registers a0 to a7)
        for (int i = 0; i < obj.NPars; i++)
        {
            funcAsm.Add($"\tsw a{i}, {stackFrameSize - 8 - i * 4}(sp)");
        }

        // set frame pointer (new fp = old sp)
        funcAsm.Add($"\taddi fp, sp, {stackFrameSize}");
    }

    public void GenFuncEpilogue(Obj obj)
    {
        var funcAsm = Functions[obj.Name];

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
    
    // Stack frame size = (num_locals × 8) + 8, aligned to 16 bytes.
    // Note: all locals treated as 8 bytes wide for simplicity.
    private int CalculateStackFrameSize(int numOfLocals)
    {
        var space = numOfLocals * 8 + 8;
        // allocate stack frame (needs to be 16 byte aligned according to ABI spec)
        return (int)(Math.Ceiling(space / 16.0) * 16);
    }
}