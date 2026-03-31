namespace PrintIt.Domain.Entities;

public class Color
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; } = StoreConstants.BootstrapStoreId;
    public Store Store { get; set; } = null!;

    // Black / White...
    public string Name { get; set; } = string.Empty;

    // Optional for UI: "#000000"
    public string? Hex { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
