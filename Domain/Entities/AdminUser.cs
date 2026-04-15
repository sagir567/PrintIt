namespace PrintIt.Domain.Entities;

public class AdminUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StoreId { get; set; } = StoreConstants.BootstrapStoreId;
    public Store Store { get; set; } = null!;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty; 

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }
}