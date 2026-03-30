namespace PrintIt.Domain.Entities;

public class MaterialType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; } = StoreConstants.BootstrapStoreId;
    public Store Store { get; set; } = null!;

    // PLA / PETG / ABS...
    public string Name { get; set; } = string.Empty;

    // אדמין בלבד; הלקוח פשוט לא רואה לא-פעילים
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
