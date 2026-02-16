namespace PrintIt.Domain.Entities;

public class MaterialType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // PLA / PETG / ABS...
    public string Name { get; set; } = string.Empty;

    // אדמין בלבד; הלקוח פשוט לא רואה לא-פעילים
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
