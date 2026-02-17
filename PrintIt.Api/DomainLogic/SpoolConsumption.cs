using PrintIt.Domain.Entities;

namespace PrintIt.Api.DomainLogic;

public static class SpoolConsumption
{
    public static bool CanConsume(FilamentSpool spool, int gramsUsed, int toleranceGrams)
        => gramsUsed <= spool.RemainingGrams + toleranceGrams;

    public static void Apply(FilamentSpool spool, int gramsUsed)
    {
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
