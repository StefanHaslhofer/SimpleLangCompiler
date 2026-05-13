namespace SimpleLangCompiler.Symtab;

public enum OperandKind
{
    Var,
    Func,
    Val,
    None
}

public class Operand(Struct s, OperandKind kind)
{
    public readonly Struct Struct = s;
    public OperandKind Kind = kind;
}