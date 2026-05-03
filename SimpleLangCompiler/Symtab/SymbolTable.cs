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
    
    public void CloseScope(){
        CurScope = CurScope.Outer;
    }
}