namespace SimpleLangCompiler.Symtab;

/// <summary>
/// Symbol table scopes.
/// </summary>
public class Scope(Scope outer)
{
    /// <summary>
    /// Reference to the outer scope.
    /// </summary>
    public Scope Outer = outer;

    /// <summary>
    /// List of objects in the scope.
    /// </summary>
    private LinkedList<Obj> Locals = new();

    /// <summary>
    /// Number of variables in the scope (used for addressing later).
    /// </summary>
    private int NVars { get; set; } = 0;
}