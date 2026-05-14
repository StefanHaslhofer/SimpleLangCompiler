using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.FrontEnd;

public class Parser
{
    const bool _T = true;
    const bool _x = false;
    const int minErrDist = 2;

    public Scanner scanner;
    public Errors errors;
    public readonly SymbolTable SymTab;

    public Token t;   // last recognized token
    public Token la;  // lookahead token
    int errDist = minErrDist;

    public Parser(Scanner scanner)
    {
        this.scanner = scanner;
        SymTab = new SymbolTable(this);
        errors = new Errors();
    }

    void SynErr(int n)
    {
        if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
        errDist = 0;
    }

    public void SemErr(string msg)
    {
        if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
        errDist = 0;
    }

    void Get()
    {
        for (;;)
        {
            t = la;
            la = scanner.Scan();
            if (la.kind <= TokenKind.NoSym) { ++errDist; break; }
            la = t;
        }
    }

    void Expect(TokenKind n)
    {
        if (la.kind == n) Get(); else SynErr((int)n);
    }

    bool StartOf(int s)
    {
        return set[s, (int)la.kind];
    }

    void ExpectWeak(TokenKind n, int follow)
    {
        if (la.kind == n) Get();
        else
        {
            SynErr((int)n);
            while (!StartOf(follow)) Get();
        }
    }

    bool WeakSeparator(TokenKind n, int syFol, int repFol)
    {
        TokenKind kind = la.kind;
        if (kind == n) { Get(); return true; }
        else if (StartOf(repFol)) { return false; }
        else
        {
            SynErr((int)n);
            while (!(set[syFol, (int)kind] || set[repFol, (int)kind] || set[0, (int)kind]))
            {
                Get();
                kind = la.kind;
            }
            return StartOf(syFol);
        }
    }

    void SimpleLang()
    {
        Declaration();
        while (la.kind == TokenKind.Var || la.kind == TokenKind.Fn)
        {
            Declaration();
        }
    }

    void Declaration()
    {
        if (la.kind == TokenKind.Var)
            VarDecl();
        else if (la.kind == TokenKind.Fn)
            FnDecl();
        else
            SynErr(30);
    }

    void VarDecl()
    {
        var kind = ObjKind.Var;
        Expect(TokenKind.Var);
        Expect(TokenKind.Ident);
        var name = t.val;

        Expect(TokenKind.Colon);
        var type = Type();
        Expect(TokenKind.Semicolon);

        SymTab.Insert(kind, name, type);
    }

    void FnDecl()
    {
        var kind = ObjKind.Func;
        Expect(TokenKind.Fn);
        Expect(TokenKind.Ident);
        var name = t.val;
        var obj = SymTab.Insert(kind, name, null);

        SymTab.OpenScope();
        SymTab.CurFnc = obj;
        var returnType = Parameters();

        Expect(TokenKind.LBrace);
        while (la.kind == TokenKind.Var)
        {
            VarDecl();
        }

        obj.Locals = SymTab.CurScope.Locals;
        obj.Type = returnType;
        StatSeq();
        
        SymTab.CloseScope();
        SymTab.CurFnc = null;
        Expect(TokenKind.RBrace);
    }

    Struct Type()
    {
        Expect(TokenKind.Ident);
        var typeObj = SymTab.Find(t.val);
        return typeObj.Type!;
    }

    Struct Parameters()
    {
        Expect(TokenKind.LParen);
        if (la.kind == TokenKind.Ident)
        {
            Param();
            while (la.kind == TokenKind.Comma)
            {
                Get();
                Param();
            }
        }
        Expect(TokenKind.RParen);
        if (la.kind == TokenKind.Colon)
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
        var name = t.val;
        Expect(TokenKind.Colon);
        var type = Type();
        SymTab.Insert(kind, name, type);
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
        if (la.kind == TokenKind.Ident)
        {
            Get();
            Obj o = SymTab.Find(t.val);
            x = new Operand(o.Type!, GetOpKind(t.val));
            
            if (la.kind == TokenKind.Assign)
            {
                Get();
                Operand? y = Expression();
                if (SymTab.CheckOperandCompatibility(x, y))
                {
                    SymTab.CheckAssignability(x, y);
                }
            }
            else if (la.kind == TokenKind.LParen)
            {
                if (o.Kind != ObjKind.Func)
                {
                    SynErr(37);
                }
                ActParameters();
            }
            else SynErr(31);
            Expect(TokenKind.Semicolon);
        }
        else if (la.kind == TokenKind.If)
        {
            Get();
            Expect(TokenKind.LParen);
            Condition();
            Expect(TokenKind.RParen);
            Expect(TokenKind.LBrace);
            StatSeq();
            Expect(TokenKind.RBrace);
            while (la.kind == TokenKind.Elseif)
            {
                Get();
                Expect(TokenKind.LParen);
                Condition();
                Expect(TokenKind.RParen);
                Expect(TokenKind.LBrace);
                StatSeq();
                Expect(TokenKind.RBrace);
            }
            if (la.kind == TokenKind.Else)
            {
                Get();
                Expect(TokenKind.LBrace);
                StatSeq();
                Expect(TokenKind.RBrace);
            }
        }
        else if (la.kind == TokenKind.While)
        {
            Get();
            Expect(TokenKind.LParen);
            Condition();
            Expect(TokenKind.RParen);
            Expect(TokenKind.LBrace);
            StatSeq();
            Expect(TokenKind.RBrace);
        }
        else if (la.kind == TokenKind.Return)
        {
            Get();
            x = new Operand(SymTab.VoidType, OperandKind.None);
            if (StartOf(2))
            {
                x = Expression();
            }

            SymTab.CheckFunctionReturn(x, SymTab.CurFnc!);
            
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
        if (la.kind == TokenKind.Plus || la.kind == TokenKind.Minus)
        {
            Addop();
        }
        Operand x = Term();
        while (la.kind == TokenKind.Plus || la.kind == TokenKind.Minus)
        {
            Addop();
            Operand y = Term();
            SymTab.CheckOperandCompatibility(x, y);
        }

        return x;
    }

    void ActParameters()
    {
        Obj fnc = SymTab.Find(t.val);
        Expect(TokenKind.LParen);
        if (StartOf(2))
        {
            Operand x = Expression();
            int expr = 1;
            // first n (= obj.NPars) locals are parameters
            LinkedListNode<Obj>? arg = fnc.Locals.First;
            if (arg != null && !SymTab.IsTypeCompatibleTo(x.Struct, arg.Value.Type)) 
                SemErr(Errors.WrongArgumentType);
            
            while (la.kind == TokenKind.Comma)
            {
                Get();
                x = Expression();
                expr++;
                
                arg = arg?.Next;
                if (arg != null && !SymTab.IsTypeCompatibleTo(x.Struct, arg.Value.Type)) 
                    SemErr(Errors.WrongArgumentType);
            }
            
            // number of arguments must match the number of function parameters
            if (fnc != SymTab.NoObj && expr != fnc.NPars)
            {
                SemErr(Errors.WrongArgumentCount);
            }
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
        switch (la.kind)
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
        if (la.kind == TokenKind.Plus)       Get();
        else if (la.kind == TokenKind.Minus)  Get();
        else SynErr(34);
    }

    Operand Term()
    {
        Operand x = Factor();
        while (la.kind == TokenKind.Star || la.kind == TokenKind.Slash || la.kind == TokenKind.Percent)
        {
            Mulop();
            Operand y = Factor();

            if (x?.Struct.Type != StructKind.Int || y?.Struct.Type != StructKind.Int)
            {
                SemErr(Errors.IntegerNeeded);
            }
        }
        
        return x;
    }

    Operand Factor()
    {
        Operand op = new Operand(SymTab.VoidType, OperandKind.None);
        if (la.kind == TokenKind.Ident)
        {
            Get();
            Obj o = SymTab.Find(t.val);
            op = new Operand(o.Type, OperandKind.Var);
            if (la.kind == TokenKind.LParen)
            {
                if (o.Kind != ObjKind.Func)
                {
                    SynErr(37);
                }
                // operand is a function if parenthesis opens after identifier
                op.Kind = OperandKind.Func;
                ActParameters();
            } else if (o.Kind == ObjKind.Func)
            {
                SynErr(10);
            }
        }
        else if (la.kind == TokenKind.Number)
        {
            Get();
            op = new Operand(SymTab.IntType, OperandKind.Val);
        }
        else if (la.kind == TokenKind.CharCon)
        {
            Get();
            op = new Operand(SymTab.CharType, OperandKind.Val);
        }
        else if (la.kind == TokenKind.LParen)
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
        if (la.kind == TokenKind.Star)         Get();
        else if (la.kind == TokenKind.Slash)   Get();
        else if (la.kind == TokenKind.Percent) Get();
        else SynErr(36);
    }

    public void Parse()
    {
        la = new Token();
        la.val = "";
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