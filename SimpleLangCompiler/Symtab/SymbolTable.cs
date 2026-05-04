namespace SimpleLangCompiler.Symtab;

/// <summary>
/// Symbol table.
/// </summary>
public class SymbolTable
{
    public Obj putFunc, putLnFunc, ordFunc, chrFunc;
    // TODO: add all pre declared functions and types here
    public Scope CurScope { get; set; }

    public SymbolTable()
    {
        OpenScope();
        
        // register pre-declared types and functions
        // TODO
    }

    public void OpenScope()
    {
        CurScope = new Scope(CurScope);
    }
    
    /// <summary>
    /// Insert an object into the current scope.
    /// </summary>
    public void Insert(Obj obj)
    {
        CurScope.Insert(obj);
    }
    
    public void CloseScope(){
        CurScope = CurScope.Outer;
    }
}