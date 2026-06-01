using SimpleLangCompiler.FrontEnd;

var fileName = args.Length > 0 ? args[0] : "C:\\Users\\stefan.haslhofer\\Documents\\JKU\\SimpleLangCompiler\\SimpleLangCompiler\\Tests\\test2.sl";
var buildEnv = args.Length > 1 ? args[1] : "sim";

Parser parser = new Parser(new Scanner(fileName), buildEnv);
parser.Parse();

Console.Write("done");
