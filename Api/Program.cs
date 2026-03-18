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
        var pla = new PrintIt.Domain.Entities.MaterialType { Name = "PLA", IsActive = true };
        var petg = new PrintIt.Domain.Entities.MaterialType { Name = "PETG", IsActive = true };
        var abs = new PrintIt.Domain.Entities.MaterialType { Name = "ABS", IsActive = true };
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
}

public partial class Program { }
