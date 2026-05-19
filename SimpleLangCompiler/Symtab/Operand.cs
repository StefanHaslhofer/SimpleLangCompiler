namespace SimpleLangCompiler.Symtab;

public enum OperandKind
{
    Var,
    Func,
    Val,
    None
}

public enum AddressingMode
{
    Reg,
    RegRel,
    Abs
}

public class Operand(Struct s, OperandKind kind)
{
    public readonly Struct Struct = s;
    public OperandKind Kind = kind;

    // Var: name of the variable
    public string? Name;
    
    // Var: addressing mode of variable
    public AddressingMode? AddrMode;
    
    // Val: constant value
    public int? Val;
    
    // Var-Reg, Var-RegRel: register
    public int? Reg;
    
    // Var-Abs, Func: address; Var-RegRel: offset
    public int? AdrOffset;
    
    // Var-Abs, Var-RegRel: index register if not none
    public int? Idx;
    
    // Var-Abs, Var-RegRel: scale factor of index register
    public int? Scale;
}