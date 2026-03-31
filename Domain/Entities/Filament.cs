namespace PrintIt.Domain.Entities;

public class Filament
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; } = StoreConstants.BootstrapStoreId;
    public Store Store { get; set; } = null!;

    public Guid MaterialTypeId { get; set; }
    public MaterialType MaterialType { get; set; } = null!;

    public Guid ColorId { get; set; }
    public Color Color { get; set; } = null!;

    // Admin only
    public string Brand { get; set; } = string.Empty;

    // inner cost per kg
    public decimal CostPerKg { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<FilamentSpool> Spools { get; set; } = new();

}
