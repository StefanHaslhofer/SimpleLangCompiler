namespace SimpleLangCompiler.Symtab;

/// <summary>
///     Symbol table scopes.
/// </summary>
public class Scope(int level, Scope? outer)
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

    /// <summary>
    ///     Scope level: 0 = global, 1 = local
    ///     Needed to verify if an obj is defined locally or globally.
    /// </summary>
    public int Level = level;

    public void Insert(Obj obj)
    {
        // address offset is dependent on number of variables in scope
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