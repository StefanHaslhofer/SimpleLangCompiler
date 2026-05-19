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

    /// <summary>
    ///     Var: addressing mode of variable
    /// </summary>
    public AddressingMode? AddrMode;

    /// <summary>
    ///     Val: constant value
    /// </summary>
    public int? Val;

    /// <summary>
    ///     Var-Reg, Var-RegRel: register
    /// </summary>
    public int? Reg;

    /// <summary>
    ///     Var-Abs, Func: address; Var-RegRel: offset
    /// </summary>
    public int? AdrOffset;

    /// <summary>
    ///     Var-Abs, Var-RegRel: index register if not none
    /// </summary>
    public int? Idx;

    /// <summary>
    ///     Var-Abs, Var-RegRel: scale factor of index register
    /// </summary>
    public int? Scale;
}