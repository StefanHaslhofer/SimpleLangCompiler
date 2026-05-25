namespace SimpleLangCompiler.Symtab;

public enum ObjKind
{
    Var,
    Func,
    Type
}

public class Obj(ObjKind kind, string name, Struct type, int level)
{
    public string Name = name;
    public ObjKind Kind = kind;
    public Struct Type = type;
    
    // Scope level: 0 = global, 1 = local
    // Needed to verify if an obj is defined locally or globally.
    public int Level = level;
    
    // Only for Var, Fnc: address offset of the element.
    public int AdrOffset;
    
    // Only for Fnc: number of parameters.
    public int NPars;

    // Indicates whether this Var is a function parameter (requires special handling)
    public bool IsParam;
    
    public LinkedList<Obj> Locals = [];
}