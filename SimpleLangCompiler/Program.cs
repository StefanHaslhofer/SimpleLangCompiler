using SimpleLangCompiler.FrontEnd;
using SimpleLangCompiler.Symtab;

var fileName = args.Length > 0 ? args[0] : "C:\\Users\\haslh\\Documents\\JKU\\14.Semester\\AdvancedCompilerConstruction\\Project\\SimpleLangCompiler\\Tests\\test.sl";

SymbolTable symTab = new SymbolTable();
Parser parser = new Parser(new Scanner(fileName), symTab);
parser.Parse();

Console.Write("done");
