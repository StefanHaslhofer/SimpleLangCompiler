using SimpleLangCompiler.Codegen;
using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.FrontEnd;

public class Parser
{
    const bool _T = true;
    const bool _x = false;
    const int MinErrDist = 2;

    public readonly Scanner Scanner;
    public readonly Errors Errors;
    public readonly SymbolTable SymTab;
    public readonly AsmGen AsmGen;

    private Token T;   // last recognized token
    private Token La;  // lookahead token
    private int _errDist = MinErrDist;

    public Parser(Scanner scanner)
    {
        Scanner = scanner;
        SymTab = new SymbolTable(this);
        Errors = new Errors();
        AsmGen = new AsmGen(new RegisterAllocator());
    }

    void SynErr(int n)
    {
        if (_errDist >= MinErrDist) Errors.SynErr(La.line, La.col, n);
        _errDist = 0;
    }

    public void SemErr(string msg)
    {
        if (_errDist >= MinErrDist) Errors.SemErr(T.line, T.col, msg);
        _errDist = 0;
    }

    void Get()
    {
        for (;;)
        {
            T = La;
            La = Scanner.Scan();
            if (La.kind <= TokenKind.NoSym) { ++_errDist; break; }
            La = T;
        }
    }

    void Expect(TokenKind n)
    {
        if (La.kind == n) Get(); else SynErr((int)n);
    }

    bool StartOf(int s)
    {
        return set[s, (int)La.kind];
    }

    void ExpectWeak(TokenKind n, int follow)
    {
        if (La.kind == n) Get();
        else
        {
            SynErr((int)n);
            while (!StartOf(follow)) Get();
        }
    }

    bool WeakSeparator(TokenKind n, int syFol, int repFol)
    {
        TokenKind kind = La.kind;
        if (kind == n) { Get(); return true; }
        else if (StartOf(repFol)) { return false; }
        else
        {
            SynErr((int)n);
            while (!(set[syFol, (int)kind] || set[repFol, (int)kind] || set[0, (int)kind]))
            {
                Get();
                kind = La.kind;
            }
            return StartOf(syFol);
        }
    }

    void SimpleLang()
    {
        AsmGen.GenTextPrologue();
        
        Declaration();
        while (La.kind == TokenKind.Var || La.kind == TokenKind.Fn)
        {
            Declaration();
        }

        AsmGen.GenTextEpilogue();
        AsmGen.Print();
    }

    void Declaration()
    {
        if (La.kind == TokenKind.Var)
            VarDecl();
        else if (La.kind == TokenKind.Fn)
            FnDecl();
        else
            SynErr(30);
    }

    void VarDecl()
    {
        var kind = ObjKind.Var;
        Expect(TokenKind.Var);
        Expect(TokenKind.Ident);
        var name = T.val;

        Expect(TokenKind.Colon);
        var type = Type();
        Expect(TokenKind.Semicolon);

        var obj = SymTab.Insert(kind, name, type);
        AsmGen.GenVarDecl(obj);
    }

    void FnDecl()
    {
        var kind = ObjKind.Func;
        Expect(TokenKind.Fn);
        Expect(TokenKind.Ident);
        var name = T.val;
        var obj = SymTab.Insert(kind, name, null);
        
        SymTab.OpenScope();
        SymTab.CurFnc = obj;
        var returnType = Parameters();
        
        Expect(TokenKind.LBrace);
        while (La.kind == TokenKind.Var)
        {
            VarDecl();
        }
        obj.Locals = SymTab.CurScope!.Locals;
        obj.Type = returnType;
        
        AsmGen.GenFuncPrologue(obj);
        StatSeq();
        AsmGen.GenFuncEpilogue(obj);
        
        SymTab.CloseScope();
        SymTab.CurFnc = null;
        Expect(TokenKind.RBrace);
    }

    Struct Type()
    {
        Expect(TokenKind.Ident);
        var typeObj = SymTab.Find(T.val);
        return typeObj.Type!;
    }

    Struct Parameters()
    {
        Expect(TokenKind.LParen);
        if (La.kind == TokenKind.Ident)
        {
            Param();
            while (La.kind == TokenKind.Comma)
            {
                Get();
                Param();
            }
        }
        Expect(TokenKind.RParen);
        if (La.kind == TokenKind.Colon)
        {
            Get();
            return Type();
        }
        
        return SymTab.VoidType;
    }

    void Param()
    {
        var kind = ObjKind.Var;
        Expect(TokenKind.Ident);
        var name = T.val;
        Expect(TokenKind.Colon);
        var type = Type();
        var obj = SymTab.Insert(kind, name, type);
        obj.IsParam = true;
        SymTab.CurFnc!.NPars++;
    }

    void StatSeq()
    {
        Statement();
        while (StartOf(1))
        {
            Statement();
        }
    }

    void Statement()
    {
        Operand? x = null;
        if (La.kind == TokenKind.Ident)
        {
            Get();
            Obj o = SymTab.Find(T.val);
            x = new Operand(o.Type!, GetOpKind(T.val));
            
            if (La.kind == TokenKind.Assign)
            {
                Get();
                Operand? y = Expression();
                if (SymTab.CheckOperandCompatibility(x, y))
                {
                    SymTab.CheckAssignability(x, y);
                }
            }
            else if (La.kind == TokenKind.LParen)
            {
                // TODO store params in registers a0 to a7 (function params)
                //  or push directly to the stack to support >8 params
                if (o.Kind != ObjKind.Func)
                {
                    SynErr(37);
                }
                ActParameters();
            }
            else SynErr(31);
            Expect(TokenKind.Semicolon);
        }
        else if (La.kind == TokenKind.If)
        {
            Get();
            Expect(TokenKind.LParen);
            Condition();
            Expect(TokenKind.RParen);
            Expect(TokenKind.LBrace);
            StatSeq();
            Expect(TokenKind.RBrace);
            while (La.kind == TokenKind.Elseif)
            {
                Get();
                Expect(TokenKind.LParen);
                Condition();
                Expect(TokenKind.RParen);
                Expect(TokenKind.LBrace);
                StatSeq();
                Expect(TokenKind.RBrace);
            }
            if (La.kind == TokenKind.Else)
            {
                Get();
                Expect(TokenKind.LBrace);
                StatSeq();
                Expect(TokenKind.RBrace);
            }
        }
        else if (La.kind == TokenKind.While)
        {
            Get();
            Expect(TokenKind.LParen);
            Condition();
            Expect(TokenKind.RParen);
            Expect(TokenKind.LBrace);
            StatSeq();
            Expect(TokenKind.RBrace);
        }
        else if (La.kind == TokenKind.Return)
        {
            Get();
            x = new Operand(SymTab.VoidType, OperandKind.None);
            if (StartOf(2))
            {
                x = Expression();
            }

            if (SymTab.CheckFunctionReturn(x, SymTab.CurFnc!))
            {
                // TODO
                //  AsmGen.GenReturn(x, SymTab.CurFnc!);
            }
            
            Expect(TokenKind.Semicolon);
        }
        else SynErr(32);
    }

    private OperandKind GetOpKind(string val)
    {
        Obj o = SymTab.Find(val);
        
        return o.Kind switch
        {
            ObjKind.Var => OperandKind.Var,
            ObjKind.Func => OperandKind.Func,
            _ => OperandKind.None
        };
    }

    Operand Expression()
    {
        TokenKind op;
        // TODO allocate temporary registers in here somewhere to store expression intermediates 
        if (La.kind == TokenKind.Plus || La.kind == TokenKind.Minus)
        {
            Addop();
            op = T.kind;
        }
        Operand x = Term();
        while (La.kind == TokenKind.Plus || La.kind == TokenKind.Minus)
        {
            Addop();
            op = T.kind;
            Operand y = Term();
            if (SymTab.CheckOperandCompatibility(x, y))
            {
                AsmGen.GenArithmetic(op, x, y, SymTab.CurFnc);
            }
        }

        return x;
    }

    void ActParameters()
    {
        Obj fnc = SymTab.Find(T.val);
        Expect(TokenKind.LParen);
        int expr = 0;
        if (StartOf(2))
        {
            // TODO operands(=params) need to be pushed to registers a0...a7
            Operand x = Expression();
            expr++;
            // first n (= obj.NPars) locals are parameters
            LinkedListNode<Obj>? arg = fnc.Locals.First;
            if (arg != null && !SymTab.IsTypeCompatibleTo(x.Struct, arg.Value.Type)) 
                SemErr(Errors.WrongArgumentType);
            
            while (La.kind == TokenKind.Comma)
            {
                Get();
                x = Expression();
                expr++;
                
                arg = arg?.Next;
                if (arg != null && !SymTab.IsTypeCompatibleTo(x.Struct, arg.Value.Type)) 
                    SemErr(Errors.WrongArgumentType);
            }
        }
        // number of arguments must match the number of function parameters
        if (fnc != SymTab.NoObj && expr != fnc.NPars)
        {
            SemErr(Errors.WrongArgumentCount);
        }
        Expect(TokenKind.RParen);
    }

    void Condition()
    {
        Operand? x = Expression();
        Relop();
        Operand? y = Expression();

        SymTab.CheckOperandCompatibility(x, y);
    }

    void Relop()
    {
        switch (La.kind)
        {
            case TokenKind.Assign:    Get(); break;
            case TokenKind.Hash:      Get(); break;
            case TokenKind.Less:      Get(); break;
            case TokenKind.Greater:   Get(); break;
            case TokenKind.GreaterEq: Get(); break;
            case TokenKind.LessEq:    Get(); break;
            default: SynErr(33); break;
        }
    }

    void Addop()
    {
        if (La.kind == TokenKind.Plus)       Get();
        else if (La.kind == TokenKind.Minus)  Get();
        else SynErr(34);
    }

    Operand Term()
    {
        Operand x = Factor();
        while (La.kind == TokenKind.Star || La.kind == TokenKind.Slash || La.kind == TokenKind.Percent)
        {
            Mulop();
            Operand y = Factor();

            if (x?.Struct.Type != StructKind.Int || y?.Struct.Type != StructKind.Int)
            {
                SemErr(Errors.IntegerNeeded);
            }
            
            // TODO AsmGen.GenOp(op, x, y);
        }
        
        return x;
    }

    Operand Factor()
    {
        Operand op = new Operand(SymTab.VoidType, OperandKind.None);
        if (La.kind == TokenKind.Ident)
        {
            Get();
            Obj o = SymTab.Find(T.val);
            
            op = AsmGen.VarOperand(o);
            // operand is a function if parenthesis opens after identifier
            if (La.kind == TokenKind.LParen)
            {
                if (o.Kind != ObjKind.Func)
                {
                    SynErr(37);
                }
                op = AsmGen.FuncOperand(o);
                ActParameters();
            } else if (o.Kind == ObjKind.Func)
            {
                // syntax error if object is a function but is not called with parenthesis
                SynErr(10);
            }
        }
        else if (La.kind == TokenKind.Number)
        {
            Get();
            op = AsmGen.ValOperand(SymTab.IntType, int.Parse(T.val));
        }
        else if (La.kind == TokenKind.CharCon)
        {
            Get();
            // TODO check if char should also be ValOperand
            op = new Operand(SymTab.CharType, OperandKind.Val);
        }
        else if (La.kind == TokenKind.LParen)
        {
            Get();
            op = Expression();
            Expect(TokenKind.RParen);
        }
        else SynErr(35);

        return op;
    }

    void Mulop()
    {
        if (La.kind == TokenKind.Star)         Get();
        else if (La.kind == TokenKind.Slash)   Get();
        else if (La.kind == TokenKind.Percent) Get();
        else SynErr(36);
    }

    public void Parse()
    {
        La = new Token();
        La.val = "";
        Get();
        SimpleLang();
        Expect(TokenKind.Eof);
    }

    static readonly bool[,] set = {
        {_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x},
        {_x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_T,_x, _x,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x},
        {_x,_T,_T,_T, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _T,_T,_x,_x, _x,_x,_x}
    };
}

public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}