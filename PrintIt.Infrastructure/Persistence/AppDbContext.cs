using Microsoft.EntityFrameworkCore;
using PrintIt.Domain.Entities;

namespace PrintIt.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    // Constructor – EF משתמש בזה כדי "להרים" את ה־DB
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // ===== Tables =====
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<MaterialType> MaterialTypes => Set<MaterialType>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Filament> Filaments => Set<Filament>();
    public DbSet<FilamentSpool> FilamentSpools => Set<FilamentSpool>();


    // ===== Model configuration =====
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MaterialType
        modelBuilder.Entity<MaterialType>(b =>
        {
            
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            b.HasIndex(x => x.Name).IsUnique();
            b.HasQueryFilter(x => x.IsActive); // Global filter to exclude inactive items by default.
        });

        // Color
        modelBuilder.Entity<Color>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            b.Property(x => x.Hex)
                .HasMaxLength(7);

            b.HasIndex(x => x.Name).IsUnique();
        });

        // Filament (internal SKU)
        modelBuilder.Entity<Filament>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Brand)
                .IsRequired()
                .HasMaxLength(80);

            b.Property(x => x.CostPerKg)
                .HasPrecision(18, 2);

            b.HasOne(x => x.MaterialType)
                .WithMany()
                .HasForeignKey(x => x.MaterialTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Color)
                .WithMany()
                .HasForeignKey(x => x.ColorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.MaterialTypeId, x.ColorId, x.Brand })
                .IsUnique();
        });
        modelBuilder.Entity<FilamentSpool>(b =>
        {
            b.HasKey(x => x.Id);
        
            b.Property(x => x.RemainingGrams)
                .IsRequired();
        
            b.Property(x => x.InitialGrams)
                .IsRequired();
        
            b.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(20);
        
            b.HasOne(x => x.Filament)
                .WithMany(x => x.Spools)
                .HasForeignKey(x => x.FilamentId)
                .OnDelete(DeleteBehavior.Cascade);
        
            b.HasIndex(x => x.FilamentId);
        });
        

        // Product
        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(x => x.BasePrice)
                .HasPrecision(18, 2);

            b.HasMany(x => x.Variants)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ProductVariant
        modelBuilder.Entity<ProductVariant>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.SizeLabel)
                .HasMaxLength(50);

            b.Property(x => x.PriceDelta)
                .HasPrecision(18, 2);

            b.HasOne(x => x.MaterialType)
                .WithMany()
                .HasForeignKey(x => x.MaterialTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Color)
                .WithMany()
                .HasForeignKey(x => x.ColorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new
            {
                x.ProductId,
                x.MaterialTypeId,
                x.ColorId,
                x.SizeLabel
            }).IsUnique();
        });
    }
}
