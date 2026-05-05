namespace SimpleLangCompiler.Symtab;

public enum ObjKind
{
    Var,
    Func,
    Type
}

public class Obj(ObjKind kind, string name, Struct type)
{
    public string Name = name;
    public ObjKind Kind = kind;
    public Struct Type = type;
    
    /// <summary>
    /// Only for Var, Fnc: address offset of the element.
    /// </summary>
    public int AdrOffset;

    public LinkedList<Obj> Locals;
}