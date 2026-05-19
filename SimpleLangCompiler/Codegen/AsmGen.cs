using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.Codegen;

public class AsmGen(RegisterAllocator regAlloc)
{
    // Holds the lines of the .data segment.
    // Each entry represents one assembler label.
    public readonly List<string> DataSegment = [];

    // Maps a function name to its list of assembler instructions.
    // Each entry in the list represents one instruction.
    public readonly Dictionary<string, List<string>> Functions = [];

    private readonly RegisterAllocator _regAlloc = regAlloc;

    public void Print()
    {
        // TODO this method should write to a file or at least should produce output that can be automatically linked
        foreach (var s in DataSegment)
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
        }
        else
        {
            // local variable
        }

        return x;
    }

    public Operand FuncOperand(Obj o)
    {
        Operand x = new Operand(o.Type, OperandKind.Func);


        return x;
    }
}