using SimpleLangCompiler.FrontEnd;

namespace SimpleLangCompiler.Symtab;

/// <summary>
/// Symbol table.
/// </summary>
public class SymbolTable
{
    public readonly Struct VoidType = new(StructKind.Void);
    public readonly Struct IntType = new(StructKind.Int);
    public readonly Struct CharType = new(StructKind.Char);
    
    public Obj NoObj, PutFunc, PutLnFunc, OrdFunc, ChrFunc;
    // TODO: add all pre declared functions and types here
    public Scope? CurScope { get; set; }

    private readonly Parser _parser;

    /// <summary>
    ///     Register pre-declared types and functions.
    /// </summary>
    public SymbolTable(Parser parser)
    {
        _parser = parser;
        
        OpenScope();
        Insert(ObjKind.Type, "int", IntType);
        Insert(ObjKind.Type, "char", CharType);
        Insert(ObjKind.Type, "void", VoidType);
        
        NoObj = Insert(ObjKind.Var, "none", VoidType);
        
        // put
        PutFunc = Insert(ObjKind.Func, "put", VoidType);
        OpenScope();
        Insert(ObjKind.Var, "e", CharType);
        PutFunc.Locals = CurScope!.Locals;
        CloseScope();
        
        // putLn
        PutLnFunc = Insert(ObjKind.Func, "putLn", VoidType);
        
        // ord
        OrdFunc = Insert(ObjKind.Func, "ord", IntType);
        OpenScope();
        Insert(ObjKind.Var, "ch", CharType);
        OrdFunc.Locals = CurScope!.Locals;
        CloseScope();
        
        // chr
        ChrFunc = Insert(ObjKind.Func, "chr", CharType);
        OpenScope();
        Insert(ObjKind.Var, "i", IntType);
        ChrFunc.Locals = CurScope!.Locals;
        CloseScope();
    }

    /// <summary>
    ///     Finds an object in the current or enclosing scopes.
    /// </summary>
    public Obj Find(string name)
    {
        var obj = CurScope?.GetGlobal(name);
        if (obj == null)
        {
            _parser.SemErr($"{name} not found.");
            return NoObj;
        }
        
        return obj;
    }
    
    public void OpenScope()
    {
        CurScope = new Scope(CurScope);
    }
    
    /// <summary>
    ///     Insert an object into the current scope.
    /// </summary>
    public Obj Insert(ObjKind kind, string name, Struct type)
    {
        // semantic error if object already exists in scope
        if (CurScope!.FindLocal(name))
        {
            _parser.SemErr($"{name} already exists in this scope.");
        }
        
        var obj = new Obj(kind, name, type)
        {
            // address offset is dependent on number of variables in scope
            AdrOffset = CurScope.NVars
        };
        CurScope.Insert(obj);
        return obj;
    }
    
    public void CloseScope(){
        CurScope = CurScope?.Outer;
    }
}