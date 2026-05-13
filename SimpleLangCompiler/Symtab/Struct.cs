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
public class Struct(StructKind type)
{
    // currently only kind needed because classes are not supported by the grammar
    public StructKind Type = type;
}