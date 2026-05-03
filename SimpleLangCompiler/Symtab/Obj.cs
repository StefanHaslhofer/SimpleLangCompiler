namespace SimpleLangCompiler.Symtab;

public enum Kind
{
    Var,
    Func,
    Type
}

public class Obj
{
    public string Name;
    public Kind Kind;
    public Struct Type;
    
    /// <summary>
    /// Only for Var, Fnc: address offset of the element.
    /// </summary>
    public int Adr;
}