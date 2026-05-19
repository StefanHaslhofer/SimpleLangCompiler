using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.Codegen;

public class CodeGenerator
{
    // RISC-V ABI register numbers
    public const int ZERO = 0, RA = 1, SP = 2, GP = 3, TP = 4;
    public const int T0 = 5, T1 = 6, T2 = 7;
    public const int FP = 8;
    public const int S1 = 9;
    public const int A0 = 10, A1 = 11; // function arguments + return values
    public const int A2 = 12, A3 = 13, A4 = 14, A5 = 15, A6 = 16, A7 = 17; // function arguments
    public const int S2 = 18, S3 = 19, S4 = 20, S5 = 21, S6 = 22, S7 = 23, S8 = 24, S9 = 25, S10 = 26, S11 = 27;
    public const int T3 = 28, T4 = 29, T5 = 30, T6 = 31;

    private byte[] Buffer = new byte[3000];
    public int Pc = 0;

    public void Put(int x)
    {
        Buffer[Pc++] = (byte)x;
    }
    
    public void Put2 (int x) {
        // TODO test what this does
        Put(x); Put(x >> 8); // little endian order
    }
    
    public void Put4 (int x) {
        Put2(x); Put2(x >> 16);
    }

    public Operand VarOperand(Obj o)
    {
        // TODO discuss with Tschoni how to determine param offset 
        Operand x = new Operand(o.Type, OperandKind.Var);
        if (o.Level == 0)
        {
            // global variable
            x.AddrMode = AddressingMode.Abs;
            x.AdrOffset = o.AdrOffset;
        }
        
        
        return x;
    }
    
    public Operand FuncOperand(Obj o)
    {
        Operand x = new Operand(o.Type, OperandKind.Func);
        
        
        return x;
    }
}