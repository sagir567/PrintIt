namespace PrintIt.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; } = StoreConstants.BootstrapStoreId;
    public Store Store { get; set; } = null!;

    /// <summary>
    /// Display name for the product (e.g., "Desk Cable Organizer")
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly identifier (e.g., "desk-cable-organizer")
    /// Must be unique in the database.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Long-form product description for the catalog listing.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Main product image URL for the catalog.
    /// </summary>
    public string? MainImageUrl { get; set; }

    /// <summary>
    /// Controls visibility in the public catalog.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Variants of this product (size, material, color combinations).
    /// </summary>
    public List<ProductVariant> Variants { get; set; } = new();

    /// <summary>
    /// Product categories for catalog browsing/filtering.
    /// </summary>
    public List<Category> Categories { get; set; } = new();
}
