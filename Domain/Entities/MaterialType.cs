namespace PrintIt.Domain.Entities;

public class MaterialType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; } = StoreConstants.BootstrapStoreId;
    public Store Store { get; set; } = null!;

    /// <summary>
    /// Material name (e.g., "PLA", "PETG", "ABS")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base price per kilogram in the local currency.
    /// Used in product variant pricing: (WeightGrams / 1000) * BasePricePerKg + variant.PriceOffset
    /// </summary>
    public decimal BasePricePerKg { get; set; }

    /// <summary>
    /// Controls visibility in the public catalog.
    /// Must be checked explicitly in queries - filtering is NOT done automatically.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
