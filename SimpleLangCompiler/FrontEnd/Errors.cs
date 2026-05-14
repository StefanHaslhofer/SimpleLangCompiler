namespace SimpleLangCompiler.FrontEnd;

public class Errors {
	public int count = 0;                                    // number of overall errors detected
	public int synCount = 0;								 // number of syntax errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	private string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

    public const string IntegerNeeded = "operands must be of type int";
    public const string DifferentTypes = "operands must be of same type";
    public const string NotAssignable = "value cannot be assigned";
    public const string NoFuncAssignment = "assignment to a function is not allowed";
    public const string UnexpectedOperand = "value, variable or function call expected";
    public const string UnexpectedReturn = "void function must not return value";
    public const string WrongReturnType = "return type does not match function type";
    public const string MissingReturnValue = "missing return value";
    public const string WrongArgumentType = "argument type mismatch";
    
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
			case 37: s = "invalid function call"; break;

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