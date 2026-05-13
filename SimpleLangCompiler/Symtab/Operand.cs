namespace SimpleLangCompiler.Symtab;

public enum OperandKind
{
    Var,
    Func,
    Val
}

public class Operand(Struct s, OperandKind kind)
{
    public readonly Struct Struct = s;
    public readonly OperandKind Kind = kind;
}