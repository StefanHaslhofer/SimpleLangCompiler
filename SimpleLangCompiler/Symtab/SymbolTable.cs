using SimpleLangCompiler.FrontEnd;

namespace SimpleLangCompiler.Symtab;

/// <summary>
/// Symbol table.
/// </summary>
public class SymbolTable
{
    /// <summary>
    ///     Global type declarations.
    /// </summary>
    public readonly Struct VoidType = new(StructKind.Void);
    public readonly Struct IntType = new(StructKind.Int);
    public readonly Struct CharType = new(StructKind.Char);
    
    /// <summary>
    ///     Global function and variable declarations.
    /// </summary>
    public Obj NoObj, PutFunc, PutLnFunc, OrdFunc, ChrFunc;

    /// <summary>
    ///     Currently opened scope.
    /// </summary>
    public Scope? CurScope;

    /// <summary>
    ///     Sets the scope of the function currently being analyzed,
    ///     primarily used for return type validation.
    /// </summary>
    public Obj? CurFnc;

    private readonly Parser _parser;

    /// <summary>
    ///     Register pre-declared types and functions.
    /// </summary>
    public SymbolTable(Parser parser)
    {
        _parser = parser;
        
        // TODO this should be done in constructor of Parser
        OpenScope();
        Insert(ObjKind.Type, "int", IntType);
        Insert(ObjKind.Type, "char", CharType);
        Insert(ObjKind.Type, "void", VoidType);
        
        NoObj = Insert(ObjKind.Var, "none", VoidType);
        
        // put
        PutFunc = Insert(ObjKind.Func, "put", VoidType);
        OpenScope();
        Insert(ObjKind.Var, "e", CharType);
        PutFunc.NPars++;
        PutFunc.Locals = CurScope!.Locals;
        _parser.AsmGen.GenPutFunc(PutFunc);
        CloseScope();
        
        // putLn
        PutLnFunc = Insert(ObjKind.Func, "putLn", VoidType);
        _parser.AsmGen.GenPutLnFunc(PutLnFunc);
        
        // ord
        OrdFunc = Insert(ObjKind.Func, "ORD", IntType);
        OpenScope();
        Insert(ObjKind.Var, "ch", CharType);
        OrdFunc.NPars++;
        OrdFunc.Locals = CurScope!.Locals;
        CloseScope();
        
        // chr
        ChrFunc = Insert(ObjKind.Func, "CHR", CharType);
        OpenScope();
        Insert(ObjKind.Var, "i", IntType);
        ChrFunc.NPars++;
        ChrFunc.Locals = CurScope!.Locals;
        CloseScope();
    }

    /// <summary>
    ///     Finds an object in the current or enclosing scopes.
    /// </summary>
    public Obj Find(string name)
    {
        var obj = CurScope.GetGlobal(name);
        if (obj == null)
        {
            _parser.SemErr($"{name} not found.");
            return NoObj;
        }
        
        return obj;
    }
    
    public void OpenScope()
    {
        CurScope = new Scope(CurScope != null ? CurScope.Level + 1 : 0, CurScope);
    }
    
    /// <summary>
    ///     Insert an object into the current scope.
    /// </summary>
    public Obj Insert(ObjKind kind, string name, Struct? type)
    {
        // semantic error if object already exists in scope
        if (CurScope!.FindLocal(name))
        {
            _parser.SemErr($"{name} already exists in this scope.");
        }

        var obj = new Obj(kind, name, type, CurScope.Level);
        
        CurScope.Insert(obj);
        return obj;
    }
    
    public void CloseScope(){
        CurScope = CurScope!.Outer!;
    }
    
    /// <summary>
    ///     Semantic error if operand types are not compatible. 
    /// </summary>
    public bool CheckOperandCompatibility(Operand? x, Operand? y)
    {
        if (!IsTypeCompatibleTo(x?.Struct ?? VoidType, y?.Struct ?? VoidType) 
            || x?.Struct == VoidType || y?.Struct == VoidType)
        {
            _parser.SemErr(Errors.DifferentTypes);
            return false;
        }

        return true;
    }
    
    /// <summary>
    ///     Return true if operand types are equal, false otherwise.
    /// </summary>
    public bool IsTypeCompatibleTo(Struct x, Struct y)
    {
        return x.Type == y.Type;
    }
    
    /// <summary>
    ///     Semantic error if operand y is not assignable to x. 
    /// </summary>
    public bool CheckAssignability(Operand? x, Operand? y)
    {
        if(x == null || y == null || x.Kind == OperandKind.None || y.Kind == OperandKind.None)
        {
            _parser.SemErr(Errors.UnexpectedOperand);
            return false;
        }
        
        if (x.Kind == OperandKind.Func)
        {
            _parser.SemErr(Errors.NoFuncAssignment);
            return false;
            
        }
        
        return true;
    }
    
    public bool CheckFunctionReturn(Operand x, Obj fnc)
    {
        if (x.Struct == VoidType && fnc.Type != VoidType)
        {
            _parser.SemErr(Errors.MissingReturnValue);
            return false;
        }

        if (x.Struct != VoidType && fnc.Type == VoidType)
        {
            _parser.SemErr(Errors.UnexpectedReturnValue);
            return false;
        }
        
        if (!IsTypeCompatibleTo(x.Struct, fnc.Type))
        {
            _parser.SemErr(Errors.WrongReturnType);
            return false;
        }

        return true;
    }
}