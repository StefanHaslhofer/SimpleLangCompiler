namespace SimpleLangCompiler.Codegen;

// RISC-V ABI register numbers
public enum Register
{
    // special registers
    ZERO = 0,
    RA = 1,
    SP = 2,
    GP = 3,
    TP = 4,
    FP = 8,
    S1 = 9,

    // function arguments + return values
    A0 = 10,
    A1 = 11,

    // function arguments
    A2 = 12,
    A3 = 13,
    A4 = 14,
    A5 = 15,
    A6 = 16,
    A7 = 17,


    S2 = 18,
    S3 = 19,
    S4 = 20,
    S5 = 21,
    S6 = 22,
    S7 = 23,
    S8 = 24,
    S9 = 25,
    S10 = 26,
    S11 = 27,

    // temporary
    T0 = 5,
    T1 = 6,
    T2 = 7,
    T3 = 28,
    T4 = 29,
    T5 = 30,
    T6 = 31
}

public class RegisterAllocator
{
    private readonly Stack<Register> _availableTempRegs = new([
        Register.T6, Register.T5, Register.T4, Register.T3,
        Register.T2, Register.T1, Register.T0
    ]);

    private readonly Stack<Register> _availableParamRegs = new([
        Register.A0, Register.A1, Register.A2, Register.A3, Register.A4, Register.A5, Register.A6, Register.A7
    ]);

    private readonly HashSet<Register> _allocated = new();

    private bool IsTempReg(Register reg) =>
        reg is Register.T0 or Register.T1 or Register.T2 or Register.T3
            or Register.T4 or Register.T5 or Register.T6;

    private bool IsParamReg(Register reg) =>
        reg is Register.A0 or Register.A1 or Register.A2 or Register.A3
            or Register.A4 or Register.A5 or Register.A6 or Register.A7;

    /// <summary>
    ///     Allocates a register. Returns false if none are available.
    /// </summary>
    public bool TryAlloc(bool isParam, out Register reg)
    {
        var pool = isParam ? _availableParamRegs : _availableTempRegs;

        if (pool.Count == 0)
        {
            reg = default;
            return false;
        }

        // pop latest register from stack and allocate it
        reg = pool.Pop();
        _allocated.Add(reg);
        return true;
    }

    public Register Alloc(bool isParam)
    {
        if (!TryAlloc(isParam, out var reg))
        {
            // TODO add to error list here instead of throwing an error
            throw new Exception($"No registers available to allocate.");
        }

        return reg;
    }

    /// <summary>
    ///     Deallocate a register.
    /// </summary>
    public void Free(Register reg)
    {
        if (!_allocated.Remove(reg))
        {
            // TODO add to error list here instead of throwing an error
            throw new Exception($"Cannot deallocate register {reg}.");
        }

        // push register to the available stack again
        if (IsParamReg(reg))
        {
            _availableParamRegs.Push(reg);
        }
        else if (IsTempReg(reg))
        {
            _availableTempRegs.Push(reg);
        }
    }

    /// <summary>
    ///     Deallocate all registers.
    /// </summary>
    public void FreeAll()
    {
        foreach (var reg in _allocated)
        {
            Free(reg);
        }
    }

    /// <summary>
    ///     Returns true if a register is allocated, false otherwise.
    /// </summary>
    public bool IsAllocated(Register r)
    {
        return _allocated.Contains(r);
    }
}