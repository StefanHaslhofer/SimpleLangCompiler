var fileName = args.Length > 0 ? args[0] : "C:\\Users\\haslh\\Documents\\JKU\\14.Semester\\AdvancedCompilerConstruction\\Project\\SimpleLangCompiler\\Tests\\test.sl";

Parser parser = new Parser(new Scanner(fileName));
parser.Parse();

Console.Write("done");
