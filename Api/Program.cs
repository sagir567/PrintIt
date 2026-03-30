using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrintIt.Api;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StoreBootstrapSettings>(builder.Configuration.GetSection(StoreBootstrapSettings.SectionName));

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
    var storeBootstrapSettings = scope.ServiceProvider.GetRequiredService<IOptions<StoreBootstrapSettings>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StoreBootstrap");

    db.Database.Migrate();

    var bootstrapStoreId = await SeedBootstrapStoreAsync(db, storeBootstrapSettings, logger);

    // Seed initial data if empty
    await SeedDataAsync(db, bootstrapStoreId);
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

async Task<Guid> SeedBootstrapStoreAsync(AppDbContext db, StoreBootstrapSettings settings, ILogger logger)
{
    var storeId = StoreConstants.BootstrapStoreId;
    var configuredName = (settings.Name ?? string.Empty).Trim();
    var configuredSlug = (settings.Slug ?? string.Empty).Trim().ToLowerInvariant();

    var name = string.IsNullOrWhiteSpace(configuredName)
        ? StoreConstants.BootstrapStoreName
        : configuredName;

    var slug = string.IsNullOrWhiteSpace(configuredSlug)
        ? StoreConstants.BootstrapStoreSlug
        : configuredSlug;

    var existing = await db.Stores.FirstOrDefaultAsync(x => x.Id == storeId);
    if (existing is null)
    {
        db.Stores.Add(new Store
        {
            Id = storeId,
            Name = name,
            Slug = slug,
            IsActive = true
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Bootstrap store created: {StoreId} ({Slug}).", storeId, slug);
        return storeId;
    }

    var changed = false;
    if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
    {
        existing.Name = name;
        changed = true;
    }

    if (!string.Equals(existing.Slug, slug, StringComparison.Ordinal))
    {
        existing.Slug = slug;
        changed = true;
    }

    if (!existing.IsActive)
    {
        existing.IsActive = true;
        changed = true;
    }

    if (changed)
    {
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    return storeId;
}

async Task SeedDataAsync(AppDbContext db, Guid storeId)
{
    // Seed MaterialTypes
    if (!await db.MaterialTypes.AnyAsync(x => x.StoreId == storeId))
    {
        var pla = new MaterialType { StoreId = storeId, Name = "PLA", IsActive = true };
        var petg = new MaterialType { StoreId = storeId, Name = "PETG", IsActive = true };
        var abs = new MaterialType { StoreId = storeId, Name = "ABS", IsActive = true };
        db.MaterialTypes.AddRange(pla, petg, abs);
        await db.SaveChangesAsync();
    }

    // Seed Colors
    if (!await db.Colors.AnyAsync(x => x.StoreId == storeId))
    {
        var red = new Color { StoreId = storeId, Name = "Red", Hex = "#FF0000", IsActive = true };
        var blue = new Color { StoreId = storeId, Name = "Blue", Hex = "#0000FF", IsActive = true };
        var black = new Color { StoreId = storeId, Name = "Black", Hex = "#000000", IsActive = true };
        var white = new Color { StoreId = storeId, Name = "White", Hex = "#FFFFFF", IsActive = true };
        db.Colors.AddRange(red, blue, black, white);
        await db.SaveChangesAsync();
    }

    // Seed Filaments (add for materials that don't have them)
    var materials = await db.MaterialTypes.Where(x => x.StoreId == storeId).ToListAsync();
    var colors = await db.Colors.Where(x => x.StoreId == storeId).ToListAsync();

    foreach (var material in materials)
    {
        if (!await db.Filaments.AnyAsync(f => f.StoreId == storeId && f.MaterialTypeId == material.Id))
        {
            var filamentsToAdd = colors.Take(3).Select(color => new Filament
            {
                StoreId = storeId,
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
                .Where(f => f.StoreId == storeId && f.MaterialTypeId == material.Id)
                .ToListAsync();

            // Add spools
            var spools = new List<FilamentSpool>();
            foreach (var filament in filaments)
            {
                spools.Add(new FilamentSpool
                {
                    FilamentId = filament.Id,
                    RemainingGrams = 1000,
                    InitialGrams = 1000,
                    Status = "New"
                });
                spools.Add(new FilamentSpool
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
}

public partial class Program { }
