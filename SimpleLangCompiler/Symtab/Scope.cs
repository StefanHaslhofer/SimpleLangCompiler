namespace SimpleLangCompiler.Symtab;

/// <summary>
///     Symbol table scopes.
/// </summary>
public class Scope(Scope? outer)
{
    /// <summary>
    ///     Reference to the outer scope.
    /// </summary>
    public Scope? Outer = outer;

    /// <summary>
    ///     List of objects in the scope.
    /// </summary>
    public readonly LinkedList<Obj> Locals = new();

    /// <summary>
    ///     Number of variables in the scope (used for addressing later).
    /// </summary>
    public int NVars { get; set; } = 0;

    public void Insert(Obj obj)
    {
        obj.AdrOffset = NVars++;
        Locals.AddLast(obj);
    }

    public bool FindLocal(string objName)
    {
        return Locals.Any(x => x.Name == objName);
    }

    public Obj? GetGlobal(string objName)
    {
        return GetLocal(objName) ?? Outer?.GetGlobal(objName);
    }
    
    private Obj? GetLocal(string objName)
    {
        return Locals.FirstOrDefault(x => x.Name == objName);
    }
}