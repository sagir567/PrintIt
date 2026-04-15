using Microsoft.EntityFrameworkCore;
using PrintIt.Domain.Entities;

namespace PrintIt.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<MaterialType> MaterialTypes => Set<MaterialType>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Filament> Filaments => Set<Filament>();
    public DbSet<FilamentSpool> FilamentSpools => Set<FilamentSpool>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Store>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(100);

            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<AdminUser>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(256);

            b.Property(x => x.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(256);

            b.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(1000);

            b.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => x.IsActive);
            b.HasIndex(x => new { x.StoreId, x.NormalizedEmail }).IsUnique();
        });

        modelBuilder.Entity<MaterialType>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            b.Property(x => x.BasePricePerKg)
                .HasPrecision(18, 2)
                .IsRequired();

            b.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.StoreId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Color>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            b.Property(x => x.Hex)
                .HasMaxLength(7);

            b.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.StoreId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<Filament>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Brand)
                .IsRequired()
                .HasMaxLength(80);

            b.Property(x => x.CostPerKg)
                .HasPrecision(18, 2);

            b.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.MaterialType)
                .WithMany()
                .HasForeignKey(x => x.MaterialTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Color)
                .WithMany()
                .HasForeignKey(x => x.ColorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.StoreId, x.MaterialTypeId, x.ColorId, x.Brand })
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

            b.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => x.IsActive);
            b.HasIndex(x => new { x.StoreId, x.Slug }).IsUnique();

            b.HasMany(x => x.Variants)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Categories)
                .WithMany(x => x.Products)
                .UsingEntity(j => j.ToTable("ProductCategories"));
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(x => x.Description)
                .HasMaxLength(1000);

            b.HasIndex(x => x.Name).IsUnique();
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => x.IsActive);
            b.HasIndex(x => x.SortOrder);
        });

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
                x.SizeLabel,
                x.MaterialTypeId,
                x.ColorId
            }).IsUnique();

            b.HasIndex(x => x.IsActive);
        });
    }
}