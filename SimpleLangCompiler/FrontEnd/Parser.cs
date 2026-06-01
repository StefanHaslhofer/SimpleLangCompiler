using System.Text.RegularExpressions;
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

    private Token T; // last recognized token
    private Token La; // lookahead token
    private int _errDist = MinErrDist;

    public Parser(Scanner scanner, string buildEnv)
    {
        Scanner = scanner;
        Errors = new Errors();
        AsmGen = new AsmGen(new RegisterAllocator(), buildEnv);
        SymTab = new SymbolTable(this);
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

    public void Warning(string msg)
    {
        if (_errDist >= MinErrDist) Errors.Warning(T.line, T.col, $"Warning: {msg}");
        _errDist = 0;
    }

    void Get()
    {
        for (;;)
        {
            T = La;
            La = Scanner.Scan();
            if (La.kind <= TokenKind.NoSym)
            {
                ++_errDist;
                break;
            }

            La = T;
        }
    }

    void Expect(TokenKind n)
    {
        if (La.kind == n) Get();
        else SynErr((int)n);
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
        if (kind == n)
        {
            Get();
            return true;
        }
        else if (StartOf(repFol))
        {
            return false;
        }
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
            if (o.Kind == ObjKind.Var)
            {
                x = AsmGen.VarOperand(o);
            }

            if (o.Kind == ObjKind.Func)
            {
                x = AsmGen.FuncOperand(o);
            }

            if (La.kind == TokenKind.Assign)
            {
                Get();
                Operand y = Expression();
                if (SymTab.CheckOperandCompatibility(x, y) && SymTab.CheckAssignability(x, y))
                {
                    AsmGen.GenAssign(x, y, SymTab.CurFnc!);
                }
            }
            else if (La.kind == TokenKind.LParen)
            {
                if (o.Kind != ObjKind.Func)
                {
                    SynErr(37);
                }

                List<Operand> args = ActParameters();
                AsmGen.GenFuncCall(SymTab.CurFnc!, x, args);
                if (o.Type != SymTab.VoidType)
                {
                    Warning(Errors.ReturnValueIgnored);
                }
            }
            else SynErr(31);

            Expect(TokenKind.Semicolon);
        }
        else if (La.kind == TokenKind.If)
        {
            // Get labels: endLbl for the end of the if-else chain,
            // nextLbl for the next block (defaults to endLbl if no else).
            string endLbl = AsmGen.GetNewLabel();
            string nextLbl = AsmGen.GetNewLabel();

            Get();
            Expect(TokenKind.LParen);
            // False path: jump to next "else" block.
            Condition(true, nextLbl);
            Expect(TokenKind.RParen);
            Expect(TokenKind.LBrace);
            StatSeq();
            Expect(TokenKind.RBrace);


            // True path: jump to end if there are "else" blocks.
            if (La.kind == TokenKind.Elseif || La.kind == TokenKind.Else)
            {
                AsmGen.GenJump(endLbl, SymTab.CurFnc!);
            }

            while (La.kind == TokenKind.Elseif)
            {
                AsmGen.GenLbl(nextLbl, SymTab.CurFnc!);
                nextLbl = AsmGen.GetNewLabel();

                Get();
                Expect(TokenKind.LParen);
                // False path: jump to next "else" block.
                Condition(true, nextLbl);
                Expect(TokenKind.RParen);
                Expect(TokenKind.LBrace);
                StatSeq();
                Expect(TokenKind.RBrace);

                // True path: jump to end if there are "else" blocks.
                if (La.kind == TokenKind.Elseif || La.kind == TokenKind.Else)
                {
                    AsmGen.GenJump(endLbl, SymTab.CurFnc!);
                }
            }

            if (La.kind == TokenKind.Else)
            {
                AsmGen.GenLbl(nextLbl, SymTab.CurFnc!);

                Get();
                Expect(TokenKind.LBrace);
                StatSeq();
                Expect(TokenKind.RBrace);
            }
            else
            {
                // Emit next label as end if no "else" block is present.
                AsmGen.GenLbl(nextLbl, SymTab.CurFnc!);
            }

            // end of "if-else" chain
            AsmGen.GenLbl(endLbl, SymTab.CurFnc!);
        }
        else if (La.kind == TokenKind.While)
        {
            string startLbl = AsmGen.GetNewLabel();
            string endLbl = AsmGen.GetNewLabel();
            Get();
            Expect(TokenKind.LParen);
            // start of loop
            AsmGen.GenLbl(startLbl, SymTab.CurFnc!);
            Condition(true, endLbl);
            Expect(TokenKind.RParen);
            Expect(TokenKind.LBrace);
            StatSeq();
            // jump back to start of loop
            AsmGen.GenJump(startLbl, SymTab.CurFnc!);
            Expect(TokenKind.RBrace);
            // end of loop
            AsmGen.GenLbl(endLbl, SymTab.CurFnc!);
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
                AsmGen.GenReturn(x, SymTab.CurFnc!);
            }

            Expect(TokenKind.Semicolon);
        }
        else SynErr(32);
    }

    Operand Expression()
    {
        TokenKind op;
        if (La.kind == TokenKind.Plus || La.kind == TokenKind.Minus)
        {
            Addop();
            op = T.kind;
            // TODO negation not yet implemented
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

    List<Operand> ActParameters()
    {
        Obj fnc = SymTab.Find(T.val);
        List<Operand> ops = [];
        Expect(TokenKind.LParen);
        int expr = 0;
        if (StartOf(2))
        {
            Operand x = Expression();
            ops.Add(x);
            expr++;
            // first n (= obj.NPars) locals are parameters
            LinkedListNode<Obj>? arg = fnc.Locals.First;
            if (arg != null && !SymTab.IsTypeCompatibleTo(x.Struct, arg.Value.Type))
                SemErr(Errors.WrongArgumentType);

            while (La.kind == TokenKind.Comma)
            {
                Get();
                x = Expression();
                ops.Add(x);
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

        return ops;
    }

    void Condition(bool fjump, string lbl)
    {
        Operand x = Expression();
        Relop();
        var op = T.kind;
        Operand y = Expression();

        if (SymTab.CheckOperandCompatibility(x, y))
        {
            AsmGen.GenJcc(op, x, y, fjump, lbl, SymTab.CurFnc!);
        }
    }

    void Relop()
    {
        switch (La.kind)
        {
            case TokenKind.Assign: Get(); break;
            case TokenKind.NotEq: Get(); break;
            case TokenKind.Less: Get(); break;
            case TokenKind.Greater: Get(); break;
            case TokenKind.GreaterEq: Get(); break;
            case TokenKind.LessEq: Get(); break;
            default: SynErr(33); break;
        }
    }

    void Addop()
    {
        if (La.kind == TokenKind.Plus) Get();
        else if (La.kind == TokenKind.Minus) Get();
        else SynErr(34);
    }

    Operand Term()
    {
        Operand x = Factor();
        while (La.kind == TokenKind.Star || La.kind == TokenKind.Slash || La.kind == TokenKind.Percent)
        {
            Mulop();
            var op = T.kind;
            Operand y = Factor();

            if (x.Struct.Type != StructKind.Int || y.Struct.Type != StructKind.Int)
            {
                SemErr(Errors.IntegerNeeded);
            }

            AsmGen.GenArithmetic(op, x, y, SymTab.CurFnc);
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
                List<Operand> args = ActParameters();
                AsmGen.GenFuncCall(SymTab.CurFnc!, op, args, true);
            }
            else if (o.Kind == ObjKind.Func)
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
            if (!TryParseQuotedChar(T.val, out char ch))
            {
                SynErr(3);
            }
            op = AsmGen.ValOperand(SymTab.CharType, ch);
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
        if (La.kind == TokenKind.Star) Get();
        else if (La.kind == TokenKind.Slash) Get();
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

    // Try to parse a string consisting of a single quoted char. Note: this method is LLM generated.
    private bool TryParseQuotedChar(string s, out char ch)
    {
        ch = default;
        
        // check if the string is enclosed in single quotes
        if (s.Length < 2 || s[0] != '\'' || s[^1] != '\'')
            return false;

        // extract the content between the quotes
        string inner = s[1..^1];
        
        // if the inner content is a single character, return it
        if (inner.Length == 1)
        {
            ch = inner[0];
            return true;
        } 
        
        // if the inner content is an escape sequence (e.g., "\n"), unescape it
        if (inner.Length == 2 && inner[0] == '\\')
        {
            string unescaped = Regex.Unescape(inner);
            if (unescaped.Length == 1)
            {
                ch = unescaped[0];
                return true;
            }
        }

        return false;
    }
    
    static readonly bool[,] set =
    {
        {
            _T, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x,
            _x, _x, _x, _x
        },
        {
            _x, _T, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _T, _x, _x, _T, _T, _x, _x, _x, _x, _x, _x, _x, _x,
            _x, _x, _x, _x
        },
        {
            _x, _T, _T, _T, _x, _x, _x, _x, _x, _x, _T, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _x, _T, _T, _x,
            _x, _x, _x, _x
        }
    };
}

public class FatalError : Exception
{
    public FatalError(string m) : base(m)
    {
    }
}