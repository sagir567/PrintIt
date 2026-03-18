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

            b.Property(x => x.BasePricePerKg)
                .HasPrecision(18, 2)
                .IsRequired();

            b.HasIndex(x => x.Name).IsUnique();
            // Note: IsActive filtering must be done explicitly in queries, not via global filter
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

            b.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(x => x.Description)
                .HasMaxLength(2000);

            b.Property(x => x.MainImageUrl)
                .HasMaxLength(500);

            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => x.IsActive);

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
                .IsRequired()
                .HasMaxLength(50);

            b.Property(x => x.WidthMm)
                .IsRequired();

            b.Property(x => x.HeightMm)
                .IsRequired();

            b.Property(x => x.DepthMm)
                .IsRequired();

            b.Property(x => x.WeightGrams)
                .IsRequired();

            b.Property(x => x.PriceOffset)
                .HasPrecision(18, 2)
                .IsRequired();
            // Note: PriceOffset must be >= 0 (markup only). Validation handled at application level.

            b.HasOne(x => x.MaterialType)
                .WithMany()
                .HasForeignKey(x => x.MaterialTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Color)
                .WithMany()
                .HasForeignKey(x => x.ColorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: a product cannot have two variants with the same combination
            b.HasIndex(x => new
            {
                x.ProductId,
                x.SizeLabel,
                x.MaterialTypeId,
                x.ColorId
            }).IsUnique();

            b.HasIndex(x => x.IsActive);
        });
    }
}
