using System.Diagnostics;
using SimpleLangCompiler.FrontEnd;

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

public static class RegisterExtensions
{
    public static string ToLabel(this Register reg) =>
        reg.ToString().ToLowerInvariant();
}

public enum RegisterPool
{
    Temp,
    Param,
    Saved
}

public class RegisterAllocator
{
    private readonly Stack<Register> _availableTempRegs = new([
        Register.T6, Register.T5, Register.T4, Register.T3,
        Register.T2, Register.T1, Register.T0
    ]);

    private readonly Stack<Register> _availableParamRegs = new([
        Register.A7, Register.A6, Register.A5, Register.A4, Register.A3, Register.A2, Register.A1, Register.A0
    ]);
    
    private readonly Stack<Register> _availableSavedRegs = new([
        Register.S1, Register.S2
    ]);

    private readonly List<Register> _allocated = [];

    private bool IsTempReg(Register reg) =>
        reg is Register.T0 or Register.T1 or Register.T2 or Register.T3
            or Register.T4 or Register.T5 or Register.T6;

    public bool IsParamReg(Register reg) =>
        reg is Register.A0 or Register.A1 or Register.A2 or Register.A3
            or Register.A4 or Register.A5 or Register.A6 or Register.A7;
    
    private bool IsSavedReg(Register reg) =>
        reg is Register.S1 or Register.S2;


    // Allocates a register. Returns false if none is available.
    private bool TryAlloc(RegisterPool? rp, out Register reg)
    {
        var pool = rp switch
        {
            RegisterPool.Param => _availableParamRegs,
            RegisterPool.Saved => _availableSavedRegs,
            _ => _availableTempRegs
        };
        
        if (!pool.TryPop(out var top)) 
        {
            reg = default;
            return false; 
        }
        
        _allocated.Add(top);
        reg = top;
        return true;
    }

    public Register Alloc(RegisterPool? rp)
    {
        if (!TryAlloc(rp, out var reg))
        {
            throw new FatalError("No registers available to allocate.");
        }

        return reg;
    }
    
    // Deallocate a register.
    public void Free(Register reg)
    {
        if (!_allocated.Remove(reg))
        {
            throw new FatalError($"Cannot deallocate register {reg}.");
        }

        // push register to the available stack again
        if (IsParamReg(reg))
        {
            _availableParamRegs.Push(reg);
        }
        else if (IsTempReg(reg))
        {
            _availableTempRegs.Push(reg);
        } else if (IsSavedReg(reg))
        {
            _availableSavedRegs.Push(reg);
        }
    }


    // Deallocate registers a0 to a7.
    public void FreeAllParams()
    {
        // iterate in reverse order to push latest used registers to the available stack first
        foreach (var reg in _allocated.ToList().AsEnumerable().Reverse())
        {
            if (IsParamReg(reg)) Free(reg);
        }
    }


    // Returns true if a register is allocated, false otherwise.
    public bool IsAllocated(Register r)
    {
        return _allocated.Contains(r);
    }
}