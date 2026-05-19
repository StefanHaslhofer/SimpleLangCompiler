using SimpleLangCompiler.Symtab;

namespace SimpleLangCompiler.Codegen;

public class CodeGenerator
{
    private byte[] Buffer = new byte[3000];
    public int Pc = 0;

    public void Put(int x)
    {
        Buffer[Pc++] = (byte)x;
    }
    
    public void Put2 (int x) {
        // TODO test what this does
        Put(x); Put(x >> 8); // little endian order
    }
    
    public void Put4 (int x) {
        Put2(x); Put2(x >> 16);
    }
}