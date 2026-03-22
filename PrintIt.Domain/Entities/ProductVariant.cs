namespace PrintIt.Domain.Entities;

public class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the parent Product.
    /// </summary>
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Size descriptor (e.g., "Small", "Large", "50x50cm").
    /// Empty string if product does not have size variations.
    /// </summary>
    public string SizeLabel { get; set; } = string.Empty;

    /// <summary>
    /// Material type selected for this variant.
    /// </summary>
    public Guid MaterialTypeId { get; set; }
    public MaterialType MaterialType { get; set; } = null!;

    /// <summary>
    /// Color selected for this variant.
    /// </summary>
    public Guid ColorId { get; set; }
    public Color Color { get; set; } = null!;

    /// <summary>
    /// Physical dimensions in millimeters.
    /// These may vary by SizeLabel.
    /// </summary>
    public int WidthMm { get; set; }
    public int HeightMm { get; set; }
    public int DepthMm { get; set; }

    /// <summary>
    /// Weight in grams. Used for price calculation:
    /// Final price = (WeightGrams / 1000 * MaterialType.BasePricePerKg) + PriceOffset
    /// </summary>
    public int WeightGrams { get; set; }

    /// <summary>
    /// Fixed price markup (in local currency) for this specific variant.
    /// Added to the material-based price calculation.
    /// Must be non-negative (markup only, no discounts).
    /// </summary>
    public decimal PriceOffset { get; set; }

    /// <summary>
    /// Whether this variant is available for sale.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
