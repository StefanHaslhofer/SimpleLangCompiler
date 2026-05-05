using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.FrontEnd;

public class Parser {
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _number = 2;
	public const int _charCon = 3;
	public const int maxT = 29;

	const bool _T = true;
	const bool _x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;
	public SymbolTable SymTab;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;



	public Parser(Scanner scanner) {
		this.scanner = scanner;
		SymTab = new SymbolTable(this);
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void SimpleLang() {
		Declaration();
		while (la.kind == 4 || la.kind == 7) {
			Declaration();
		}
	}

	void Declaration() {
		if (la.kind == 4) {
			VarDecl();
		} else if (la.kind == 7) {
			FnDecl();
		} else SynErr(30);
	}
	
	void VarDecl()
	{
		var kind = ObjKind.Var;
		Expect(4);
		Expect(1); // var name
		var name = t.val;
			
		Expect(5);
		var type = Type();
		Expect(6);
		
		SymTab.Insert(kind, name, type);
	}
	
	void FnDecl() {
		var kind = ObjKind.Func;
		Expect(7);
		Expect(1);
		var name = t.val;
		var obj = SymTab.Insert(kind, name, null);
		
		SymTab.OpenScope();
		// function parameters
		var returnType = Parameters();
		
		Expect(8);
		// local function variables
		while (la.kind == 4) {
			VarDecl();
		}

		// store scope locals in parent object for easier access and better debugging
		obj.Locals = SymTab.CurScope.Locals;
		SymTab.CloseScope();
		// type comes after function declaration
		obj.Type = returnType;
		
		StatSeq();
		Expect(9);
	}

	Struct Type() {
		Expect(1);

		var typeObj = SymTab.Find(t.val);
		
		return typeObj.Type;
	}

	Struct Parameters() {
		Expect(10);
		if (la.kind == 1) {
			Param();
			while (la.kind == 11) {
				Get();
				Param();
			}
		}
		Expect(12);
		if (la.kind == 5) {
			Get();
			return Type();
		}

		return new Struct(StructKind.Void);
	}

	void StatSeq() {
		Statement();
		while (StartOf(1)) {
			Statement();
		}
	}

	void Param() {
		var kind = ObjKind.Var;
		Expect(1);
		var name = t.val;
		Expect(5);
		var type = Type();
		SymTab.Insert(kind, name, type);
	}

	void Statement() {
		if (la.kind == 1) {
			Get();
			if (la.kind == 13) {
				Get();
				Expression();
			} else if (la.kind == 10) {
				ActParameters();
			} else SynErr(31);
			Expect(6);
		} else if (la.kind == 14) {
			Get();
			Expect(10);
			Condition();
			Expect(12);
			Expect(8);
			StatSeq();
			Expect(9);
			while (la.kind == 15) {
				Get();
				Expect(10);
				Condition();
				Expect(12);
				Expect(8);
				StatSeq();
				Expect(9);
			}
			if (la.kind == 16) {
				Get();
				Expect(8);
				StatSeq();
				Expect(9);
			}
		} else if (la.kind == 17) {
			Get();
			Expect(10);
			Condition();
			Expect(12);
			Expect(8);
			StatSeq();
			Expect(9);
		} else if (la.kind == 18) {
			Get();
			if (StartOf(2)) {
				Expression();
			}
			Expect(6);
		} else SynErr(32);
	}

	void Expression() {
		if (la.kind == 24 || la.kind == 25) {
			Addop();
		}
		Term();
		while (la.kind == 24 || la.kind == 25) {
			Addop();
			Term();
		}
	}

	void ActParameters() {
		Expect(10);
		if (StartOf(2)) {
			Expression();
			while (la.kind == 11) {
				Get();
				Expression();
			}
		}
		Expect(12);
	}

	void Condition() {
		Expression();
		Relop();
		Expression();
	}

	void Relop() {
		switch (la.kind) {
			case 13: {
				Get();
				break;
			}
			case 19: {
				Get();
				break;
			}
			case 20: {
				Get();
				break;
			}
			case 21: {
				Get();
				break;
			}
			case 22: {
				Get();
				break;
			}
			case 23: {
				Get();
				break;
			}
			default: SynErr(33); break;
		}
	}

	void Addop() {
		if (la.kind == 24) {
			Get();
		} else if (la.kind == 25) {
			Get();
		} else SynErr(34);
	}

	void Term() {
		Factor();
		while (la.kind == 26 || la.kind == 27 || la.kind == 28) {
			Mulop();
			Factor();
		}
	}

	void Factor() {
		if (la.kind == 1) {
			Get();
			if (la.kind == 10) {
				ActParameters();
			}
		} else if (la.kind == 2) {
			Get();
		} else if (la.kind == 3) {
			Get();
		} else if (la.kind == 10) {
			Get();
			Expression();
			Expect(12);
		} else SynErr(35);
	}

	void Mulop() {
		if (la.kind == 26) {
			Get();
		} else if (la.kind == 27) {
			Get();
		} else if (la.kind == 28) {
			Get();
		} else SynErr(36);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		SimpleLang();
		Expect(0);
	}
	
	static readonly bool[,] set = {
		{_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x},
		{_x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_T,_x, _x,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _x,_x,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _T,_T,_x,_x, _x,_x,_x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
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