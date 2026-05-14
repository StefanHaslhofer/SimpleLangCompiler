namespace SimpleLangCompiler.Symtab;

public enum ObjKind
{
    Var,
    Func,
    Type
}

public class Obj(ObjKind kind, string name, Struct? type)
{
    public string Name = name;
    public ObjKind Kind = kind;
    public Struct? Type = type;
    
    /// <summary>
    ///     Only for Var, Fnc: address offset of the element.
    /// </summary>
    public int AdrOffset;

    /// <summary>
    ///     Only for Fnc: number of parameters.
    /// </summary>
    public int NPars;

    public LinkedList<Obj> Locals;
}