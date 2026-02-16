namespace PrintIt.Domain.Entities;

public class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    // מה שהלקוח בוחר:
    public Guid MaterialTypeId { get; set; }
    public MaterialType MaterialType { get; set; } = null!;

    public Guid ColorId { get; set; }
    public Color Color { get; set; } = null!;

    public string SizeLabel { get; set; } = "Default";

    // price delta from the base product price
    public decimal PriceDelta { get; set; }

    public int LeadTimeDays { get; set; } = 3;
}
