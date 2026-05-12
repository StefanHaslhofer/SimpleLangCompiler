using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.FrontEnd;

public class Parser
{
    const bool _T = true;
    const bool _x = false;
    const int minErrDist = 2;

    public Scanner scanner;
    public Errors errors;
    public SymbolTable SymTab;

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
        var returnType = Parameters();

        Expect(TokenKind.LBrace);
        while (la.kind == TokenKind.Var)
        {
            VarDecl();
        }

        obj.Locals = SymTab.CurScope.Locals;
        SymTab.CloseScope();
        obj.Type = returnType;

        StatSeq();
        Expect(TokenKind.RBrace);
    }

    Struct Type()
    {
        Expect(TokenKind.Ident);
        var typeObj = SymTab.Find(t.val);
        return typeObj.Type;
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
        return new Struct(StructKind.Void);
    }

    void Param()
    {
        var kind = ObjKind.Var;
        Expect(TokenKind.Ident);
        var name = t.val;
        Expect(TokenKind.Colon);
        var type = Type();
        SymTab.Insert(kind, name, type);
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
        if (la.kind == TokenKind.Ident)
        {
            Get();
            if (la.kind == TokenKind.Assign)
            {
                Get();
                Expression();
            }
            else if (la.kind == TokenKind.LParen)
            {
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
            if (StartOf(2))
            {
                Expression();
            }
            Expect(TokenKind.Semicolon);
        }
        else SynErr(32);
    }

    void Expression()
    {
        if (la.kind == TokenKind.Plus || la.kind == TokenKind.Minus)
        {
            Addop();
        }
        Term();
        while (la.kind == TokenKind.Plus || la.kind == TokenKind.Minus)
        {
            Addop();
            Term();
        }
    }

    void ActParameters()
    {
        Expect(TokenKind.LParen);
        if (StartOf(2))
        {
            Expression();
            while (la.kind == TokenKind.Comma)
            {
                Get();
                Expression();
            }
        }
        Expect(TokenKind.RParen);
    }

    void Condition()
    {
        Expression();
        Relop();
        Expression();
    }

    void Relop()
    {
        switch ((TokenKind)la.kind)
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

    void Term()
    {
        Factor();
        while (la.kind == TokenKind.Star || la.kind == TokenKind.Slash || la.kind == TokenKind.Percent)
        {
            Mulop();
            Factor();
        }
    }

    void Factor()
    {
        if (la.kind == TokenKind.Ident)
        {
            Get();
            if (la.kind == TokenKind.LParen)
                ActParameters();
        }
        else if (la.kind == TokenKind.Number)  Get();
        else if (la.kind == TokenKind.CharCon) Get();
        else if (la.kind == TokenKind.LParen)
        {
            Get();
            Expression();
            Expect(TokenKind.RParen);
        }
        else SynErr(35);
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


public class Errors {
	public int count = 0;                                    // number of overall errors detected
	public int synCount = 0;								 // number of syntax errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "number expected"; break;
			case 3: s = "charCon expected"; break;
			case 4: s = "\"var\" expected"; break;
			case 5: s = "\":\" expected"; break;
			case 6: s = "\";\" expected"; break;
			case 7: s = "\"fn\" expected"; break;
			case 8: s = "\"{\" expected"; break;
			case 9: s = "\"}\" expected"; break;
			case 10: s = "\"(\" expected"; break;
			case 11: s = "\",\" expected"; break;
			case 12: s = "\")\" expected"; break;
			case 13: s = "\"=\" expected"; break;
			case 14: s = "\"if\" expected"; break;
			case 15: s = "\"elseif\" expected"; break;
			case 16: s = "\"else\" expected"; break;
			case 17: s = "\"while\" expected"; break;
			case 18: s = "\"return\" expected"; break;
			case 19: s = "\"#\" expected"; break;
			case 20: s = "\"<\" expected"; break;
			case 21: s = "\">\" expected"; break;
			case 22: s = "\">=\" expected"; break;
			case 23: s = "\"<=\" expected"; break;
			case 24: s = "\"+\" expected"; break;
			case 25: s = "\"-\" expected"; break;
			case 26: s = "\"*\" expected"; break;
			case 27: s = "\"/\" expected"; break;
			case 28: s = "\"%\" expected"; break;
			case 29: s = "??? expected"; break;
			case 30: s = "invalid Declaration"; break;
			case 31: s = "invalid Statement"; break;
			case 32: s = "invalid Statement"; break;
			case 33: s = "invalid Relop"; break;
			case 34: s = "invalid Addop"; break;
			case 35: s = "invalid Factor"; break;
			case 36: s = "invalid Mulop"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
		synCount++;
	}

	public virtual void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public virtual void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}