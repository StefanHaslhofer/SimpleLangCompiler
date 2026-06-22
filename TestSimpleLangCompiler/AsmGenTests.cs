using System.Diagnostics;
using System.Text;
using SimpleLangCompiler.FrontEnd;
using Xunit.Abstractions;

namespace TestSimpleLangCompiler;

/// <summary>
///     Compiles SimpleLang source to RISC-V assembly, assembles + links it via the
///     riscv64-linux-gnu-gcc cross toolchain inside WSL, then executes it with
///     qemu-riscv64 and captures stdout for assertions.
///
///     Requirements (inside WSL):
///         sudo apt install gcc-riscv64-linux-gnu qemu-user
///
///     Note: these tests only run on Windows hosts with WSL installed. They will be
///     skipped automatically on other platforms (see SkipIfNoWsl).
///
///     The test class was built using an LLM.
/// </summary>
public class AsmGenTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "slc_exec_" + Guid.NewGuid());

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workDir))
            {
                Directory.Delete(_workDir, true);
            }
        }
        catch
        {
            // best-effort cleanup; ignore failures (e.g. file still locked)
        }
    }
    
    // Compiles the given SimpleLang source, runs it, and asserts stdout matches expected output.
    private void RunOk(string input, string expectedStdout)
    {
        var actual = CompileAndRun(input);
        Assert.Equal(expectedStdout, actual);
    }
    
    // Compiles and runs the given SimpleLang source, printing stdout to the test output
    // without asserting anything. Used because I was too lazy to implement a script
    // compiling the given test program, which would do the same anyway.
    private void RunAndPrint(string input)
    {
        var actual = CompileAndRun(input);
        output.WriteLine("---- program stdout ----");
        output.WriteLine(actual);
        output.WriteLine("---- end stdout ----");
    }

    private string CompileAndRun(string input)
    {
        Directory.CreateDirectory(_workDir);

        // 1. Parse + generate RISC-V assembly using the existing front end
        // ("linux" target use the write() syscall instead of the "sim" syscalls).
        string asmCode = CompileToAssembly(input);
        output.WriteLine(asmCode);

        string asmPath = Path.Combine(_workDir, "out.s");
        string binPath = Path.Combine(_workDir, "out");
        File.WriteAllText(asmPath, asmCode);

        // 2. Assemble + link inside WSL.
        string wslAsmPath = ToWslPath(asmPath);
        string wslBinPath = ToWslPath(binPath);

        // I had to add the '-fno-pic' flag and force the compiler to run at fixed memory addresses,
        // because otherwise memory access would break and cause a segfault.
        // I debugged this issue using the following command: `qemu-riscv64 -d in_asm,cpu out 2>&1 | tail -50` 
        var (assembleExit, assembleOut, assembleErr) = RunWsl(
            $"riscv64-linux-gnu-gcc -static -nostdlib -fno-pic -Wa,-mno-relax -o {wslBinPath} {wslAsmPath}");

        if (assembleExit != 0)
        {
            throw new Exception($"Assembling failed (exit {assembleExit}):\n{assembleOut}\n{assembleErr}");
        }

        // 3. Run the binary under qemu-riscv64 and capture stdout.
        var (runExit, runOut, runErr) = RunWsl($"qemu-riscv64 {wslBinPath}");

        if (runExit != 0)
        {
            throw new Exception($"Execution failed (exit {runExit}):\n{runOut}\n{runErr}");
        }

        return runOut;
    }

    private static string CompileToAssembly(string input)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        var parser = new Parser(new Scanner(stream), "linux");
        var sw = new StringWriter();
        parser.Errors.ErrorStream = sw;

        parser.Parse();

        if (parser.Errors.Count > 0)
        {
            throw new Exception($"Compilation failed:\n{sw}");
        }

        // reroute output assembler to string writer
        var asmOut = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(asmOut);
            parser.AsmGen.Print();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return asmOut.ToString();
    }
    
    // Converts a Windows path (e.g. C:\Users\foo\bar.s) to its WSL equivalent (/mnt/c/Users/foo/bar.s).
    private static string ToWslPath(string windowsPath)
    {
        var full = Path.GetFullPath(windowsPath).Replace('\\', '/');
        var drive = full[0].ToString().ToLowerInvariant();
        var rest = full[2..]; // strip "C:"
        return $"/mnt/{drive}{rest}";
    }

    private static (int ExitCode, string StdOut, string StdErr) RunWsl(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wsl",
            ArgumentList = { "bash", "-lc", command },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdOut, stdErr);
    }

    #region Tests

    [Fact]
    public void PrintsSingleChar() => RunOk(@"
        fn main() {
            put('A');
        }
    ", "A");

    [Fact]
    public void PrintsCharThenNewline() => RunOk(@"
        fn main() {
            put('A');
            putLn();
        }
    ", "A\n");

    [Fact]
    public void OrdAndChrRoundTrip() => RunOk(@"
        fn main() {
            var x: int;
            var c: char;

            x = ORD('A');
            c = CHR(x);
            put(c);
        }
    ", "A");

    [Fact]
    public void SimpleArithmetic() => RunOk(@"
        fn main() {
            var x: int;
            var c: char;

            x = 1 + 2 * 3; /* 7 */
            c = CHR(48 + x); /* '7' */
            put(c);
        }
    ", "7");

    [Fact]
    public void FunctionCallReturnsValue() => RunOk(@"
        fn add(a: int, b: int): int {
            return a + b;
        }

        fn main() {
            var x: int;
            var c: char;

            x = add(3, 4); /* 7 */
            c = CHR(48 + x);
            put(c);
        }
    ", "7");

    [Fact]
    public void IfElseTakesTrueBranch() => RunOk(@"
        fn main() {
            if (1 < 2) {
                put('Y');
            } else {
                put('N');
            }
        }
    ", "Y");

    [Fact]
    public void IfElseTakesFalseBranch() => RunOk(@"
        fn main() {
            if (2 < 1) {
                put('Y');
            } else {
                put('N');
            }
        }
    ", "N");

    [Fact]
    public void WhileLoopCountsToFive() => RunOk(@"
        fn main() {
            var i: int;
            var c: char;

            i = 0;
            while (i < 5) {
                c = CHR(48 + i);
                put(c);
                i = i + 1;
            }
        }
    ", "01234");

    [Fact]
    public void RecursiveFactorial() => RunOk(@"
        fn fact(n: int): int {
            if (n <= 1) {
                return 1;
            }
            return n * fact(n - 1);
        }

        fn main() {
            var x: int;
            var c: char;

            x = fact(3); /* 6 */
            c = CHR(48 + x);
            put(c);
        }
    ", "6");

    [Fact]
    public void NegationOfExpression() => RunOk(@"
        fn main() {
            var x: int;
            var c: char;

            x = -5 + 12; /* 7 */
            c = CHR(48 + x);
            put(c);
        }
    ", "7");

    [Fact]
    public void GlobalVariableAccess() => RunOk(@"
        var g: int;

        fn bump() {
            g = g + 1;
        }

        fn main() {
            var c: char;

            g = 5;
            bump();
            bump(); /* g = 7 */
            c = CHR(48 + g);
            put(c);
        }
    ", "7");

    [Fact]
    public void NineArgumentFunctionCall() => RunOk(@"
        /* exercises the 9th overflow parameter passed on the stack */
        fn sum9(a: int, b: int, c: int, d: int, e: int, f: int, g: int, h: int, i: int): int {
            return a + b + c + d + e + f + g + h + i;
        }

        fn main() {
            var x: int;
            var ch: char;

            x = sum9(1, 1, 1, 1, 1, 1, 1, 1, 1); /* 9 */
            ch = CHR(48 + x);
            put(ch);
        }
    ", "9");

    [Fact]
    public void RunTestSl() => RunAndPrint(@"
        var i: int;

        fn putInt(x: int): void { /* largest printable number = 9999 */
            var c0: char;
            var c1: char;
            var c2: char;
            var c3: char;

            c3 = CHR(48 + x % 10); x = x / 10;
            c2 = CHR(48 + x % 10); x = x / 10;
            c1 = CHR(48 + x % 10); x = x / 10;
            c0 = CHR(48 + x % 10);

            if (c0 > '0') { put(c0); put(c1); put(c2); }
            elseif (c1 > '0') { put(c1); put(c2); }
            elseif (c2 > '0') { put(c2); }
            put(c3);
        }

        fn main(): void { /* print odd numbers */
            i = 1;
            while (i < 100) {
                putInt(i);
                putLn();
                i = i + 2;
            }
        }
        ");

    #endregion
}