namespace PrintIt.Domain.Entities;

public class FilamentSpool
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FilamentId { get; set; }
    public Filament Filament { get; set; } = null!;

    // Current remaining weight in grams.
    public int RemainingGrams { get; set; }

    // The spool's weight when it was new (for tracking/statistics).
    public int InitialGrams { get; set; } = 1000;

    // New / Opened / Empty (can be an enum later).
    public string Status { get; set; } = "New";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
}
