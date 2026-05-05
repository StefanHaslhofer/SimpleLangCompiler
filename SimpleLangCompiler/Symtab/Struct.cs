namespace SimpleLangCompiler.Symtab;

public enum StructKind
{
    Void,
    Int,
    Char
}

/// <summary>
///     Type system of our language.
/// </summary>
public class Struct(StructKind kind)
{
    // currently only kind needed because classes are not supported by the grammar
    public StructKind Kind = kind;
}