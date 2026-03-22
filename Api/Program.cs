using Microsoft.EntityFrameworkCore;
using PrintIt.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Controllers (instead of the template Minimal API endpoints)
builder.Services.AddControllers();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext (EF Core + Postgres)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("Postgres");
    options.UseNpgsql(cs);
});

var app = builder.Build();

// Ensure database schema is created / migrated on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed initial data if empty
    await SeedDataAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

// Map controller routes
app.MapControllers();

app.Run();

async Task SeedDataAsync(AppDbContext db)
{
    // Seed MaterialTypes
    if (!await db.MaterialTypes.AnyAsync())
    {
        var pla = new PrintIt.Domain.Entities.MaterialType { Name = "PLA", BasePricePerKg = 150m, IsActive = true };
        var petg = new PrintIt.Domain.Entities.MaterialType { Name = "PETG", BasePricePerKg = 180m, IsActive = true };
        var abs = new PrintIt.Domain.Entities.MaterialType { Name = "ABS", BasePricePerKg = 200m, IsActive = true };
        db.MaterialTypes.AddRange(pla, petg, abs);
        await db.SaveChangesAsync();
    }

    // Seed Colors
    if (!await db.Colors.AnyAsync())
    {
        var red = new PrintIt.Domain.Entities.Color { Name = "Red", Hex = "#FF0000", IsActive = true };
        var blue = new PrintIt.Domain.Entities.Color { Name = "Blue", Hex = "#0000FF", IsActive = true };
        var black = new PrintIt.Domain.Entities.Color { Name = "Black", Hex = "#000000", IsActive = true };
        var white = new PrintIt.Domain.Entities.Color { Name = "White", Hex = "#FFFFFF", IsActive = true };
        db.Colors.AddRange(red, blue, black, white);
        await db.SaveChangesAsync();
    }

    // Seed Filaments (add for materials that don't have them)
    var materials = await db.MaterialTypes.ToListAsync();
    var colors = await db.Colors.ToListAsync();

    foreach (var material in materials)
    {
        if (!await db.Filaments.AnyAsync(f => f.MaterialTypeId == material.Id))
        {
            var filamentsToAdd = colors.Take(3).Select(color => new PrintIt.Domain.Entities.Filament
            {
                MaterialTypeId = material.Id,
                ColorId = color.Id,
                Brand = "Generic",
                CostPerKg = material.Name == "PLA" ? 150.00m : 200.00m,
                IsActive = true
            }).ToList();
            
            db.Filaments.AddRange(filamentsToAdd);
            await db.SaveChangesAsync();

            // Retrieve the filaments from database to get their IDs
            var filaments = await db.Filaments
                .Where(f => f.MaterialTypeId == material.Id)
                .ToListAsync();

            // Add spools
            var spools = new List<PrintIt.Domain.Entities.FilamentSpool>();
            foreach (var filament in filaments)
            {
                spools.Add(new PrintIt.Domain.Entities.FilamentSpool
                {
                    FilamentId = filament.Id,
                    RemainingGrams = 1000,
                    InitialGrams = 1000,
                    Status = "New"
                });
                spools.Add(new PrintIt.Domain.Entities.FilamentSpool
                {
                    FilamentId = filament.Id,
                    RemainingGrams = 800,
                    InitialGrams = 1000,
                    Status = "Opened"
                });
            }
            db.FilamentSpools.AddRange(spools);
            await db.SaveChangesAsync();
        }
    }

    // Seed Categories and Products (minimal but rich enough for UI/demo)
    if (!await db.Categories.AnyAsync())
    {
        db.Categories.AddRange(
            new PrintIt.Domain.Entities.Category { Name = "Toys", Slug = "toys", Description = "Playful prints", SortOrder = 1, IsActive = true },
            new PrintIt.Domain.Entities.Category { Name = "Home", Slug = "home", Description = "Home helpers", SortOrder = 2, IsActive = true },
            new PrintIt.Domain.Entities.Category { Name = "Tools", Slug = "tools", Description = "Workshop aids", SortOrder = 3, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    if (!await db.Products.AnyAsync())
    {
        var catToys = await db.Categories.FirstAsync(c => c.Slug == "toys");
        var catHome = await db.Categories.FirstAsync(c => c.Slug == "home");
        var catTools = await db.Categories.FirstAsync(c => c.Slug == "tools");

        var plaId = materials.First(m => m.Name == "PLA").Id;
        var petgId = materials.First(m => m.Name == "PETG").Id;
        var absId = materials.First(m => m.Name == "ABS").Id;
        var black = colors.First(c => c.Name == "Black").Id;
        var red = colors.First(c => c.Name == "Red").Id;
        var blue = colors.First(c => c.Name == "Blue").Id;
        var white = colors.First(c => c.Name == "White").Id;

        var products = new List<PrintIt.Domain.Entities.Product>
        {
            new()
            {
                Title = "Robot Stand",
                Slug = "robot-stand",
                Description = "Display and charge your desktop robot with cable routing.",
                MainImageUrl = "https://placehold.co/800x600/1e1e2f/ffffff?text=Robot+Stand",
                IsActive = true,
                Categories = new List<PrintIt.Domain.Entities.Category> { catToys },
                Variants = new List<PrintIt.Domain.Entities.ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = plaId, ColorId = black, WidthMm = 80, HeightMm = 60, DepthMm = 40, WeightGrams = 220, PriceOffset = 8, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = plaId, ColorId = red, WidthMm = 100, HeightMm = 80, DepthMm = 50, WeightGrams = 320, PriceOffset = 11, IsActive = true }
                }
            },
            new()
            {
                Title = "Mini Car",
                Slug = "mini-car",
                Description = "Snap-fit mini car for desk racing.",
                MainImageUrl = "https://placehold.co/800x600/1b3c59/ffffff?text=Mini+Car",
                IsActive = true,
                Categories = new List<PrintIt.Domain.Entities.Category> { catToys },
                Variants = new List<PrintIt.Domain.Entities.ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = petgId, ColorId = blue, WidthMm = 50, HeightMm = 30, DepthMm = 25, WeightGrams = 120, PriceOffset = 5, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = petgId, ColorId = red, WidthMm = 70, HeightMm = 40, DepthMm = 30, WeightGrams = 180, PriceOffset = 7, IsActive = true }
                }
            },
            new()
            {
                Title = "Cable Clip Set",
                Slug = "cable-clip",
                Description = "Pack of cable clips to tidy your workspace.",
                MainImageUrl = "https://placehold.co/800x600/123524/ffffff?text=Cable+Clip",
                IsActive = true,
                Categories = new List<PrintIt.Domain.Entities.Category> { catHome },
                Variants = new List<PrintIt.Domain.Entities.ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = plaId, ColorId = white, WidthMm = 35, HeightMm = 15, DepthMm = 12, WeightGrams = 40, PriceOffset = 2, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = plaId, ColorId = black, WidthMm = 45, HeightMm = 18, DepthMm = 14, WeightGrams = 55, PriceOffset = 3, IsActive = true }
                }
            },
            new()
            {
                Title = "Wall Hook",
                Slug = "wall-hook",
                Description = "Sturdy wall hook with hidden screw mount.",
                MainImageUrl = "https://placehold.co/800x600/4a2c2a/ffffff?text=Wall+Hook",
                IsActive = true,
                Categories = new List<PrintIt.Domain.Entities.Category> { catHome },
                Variants = new List<PrintIt.Domain.Entities.ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = petgId, ColorId = black, WidthMm = 60, HeightMm = 70, DepthMm = 20, WeightGrams = 90, PriceOffset = 4, IsActive = true }
                }
            },
            new()
            {
                Title = "Wrench Holder",
                Slug = "wrench-holder",
                Description = "Wall-mount organizer for wrenches and small tools.",
                MainImageUrl = "https://placehold.co/800x600/2f2f2f/ffffff?text=Wrench+Holder",
                IsActive = true,
                Categories = new List<PrintIt.Domain.Entities.Category> { catTools },
                Variants = new List<PrintIt.Domain.Entities.ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = absId, ColorId = black, WidthMm = 100, HeightMm = 30, DepthMm = 25, WeightGrams = 180, PriceOffset = 6, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = absId, ColorId = blue, WidthMm = 120, HeightMm = 35, DepthMm = 28, WeightGrams = 240, PriceOffset = 8, IsActive = true }
                }
            }
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
}

public partial class Program { }
