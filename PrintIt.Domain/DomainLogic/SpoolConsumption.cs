using PrintIt.Domain.Entities;

namespace PrintIt.Domain.DomainLogic;

public static class SpoolConsumption
{
    // Returns true if the requested usage can be covered by RemainingGrams + tolerance.
    public static bool CanConsume(FilamentSpool spool, int gramsUsed, int toleranceGrams)
    {
        if (spool is null) throw new ArgumentNullException(nameof(spool));
        if (gramsUsed <= 0) throw new ArgumentOutOfRangeException(nameof(gramsUsed), "gramsUsed must be positive.");
        if (toleranceGrams < 0) throw new ArgumentOutOfRangeException(nameof(toleranceGrams), "toleranceGrams cannot be negative.");

        return gramsUsed <= spool.RemainingGrams + toleranceGrams;
    }

    // Applies the consumption to the spool and updates status + LastUsedAtUtc.
    public static void Apply(FilamentSpool spool, int gramsUsed)
    {
        if (spool is null) throw new ArgumentNullException(nameof(spool));
        if (gramsUsed <= 0) throw new ArgumentOutOfRangeException(nameof(gramsUsed), "gramsUsed must be positive.");

        spool.RemainingGrams = Math.Max(0, spool.RemainingGrams - gramsUsed);
        spool.LastUsedAtUtc = DateTime.UtcNow;

        if (spool.RemainingGrams == 0)
            spool.Status = "Empty";
        else if (spool.RemainingGrams < spool.InitialGrams)
            spool.Status = "Opened";
        else
            spool.Status = "New";
    }
}
