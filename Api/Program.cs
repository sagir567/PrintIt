using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PrintIt.Api.Auth;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<AuthCookieSettings>(builder.Configuration.GetSection(AuthCookieSettings.SectionName));
builder.Services.Configure<AdminBootstrapSettings>(builder.Configuration.GetSection(AdminBootstrapSettings.SectionName));
builder.Services.Configure<StoreBootstrapSettings>(builder.Configuration.GetSection(StoreBootstrapSettings.SectionName));

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey) || jwtSettings.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
}

// Controllers (instead of the template Minimal API endpoints)
builder.Services.AddControllers();

builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var allowedOrigins = configuredOrigins
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != "*")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins.Length == 0)
        {
            allowedOrigins = new[] { "http://localhost:5173" };
        }

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrEmpty(context.Token))
                    return Task.CompletedTask;

                var cookieName = builder.Configuration[$"{AuthCookieSettings.SectionName}:Name"];
                if (string.IsNullOrWhiteSpace(cookieName))
                    cookieName = "printit_admin_auth";

                if (context.Request.Cookies.TryGetValue(cookieName, out var cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
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
    var adminBootstrapSettings = scope.ServiceProvider.GetRequiredService<IOptions<AdminBootstrapSettings>>().Value;
    var adminPasswordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminBootstrap");

    db.Database.Migrate();

    var bootstrapStoreId = await SeedBootstrapStoreAsync(db, storeBootstrapSettings, logger);

    // Seed initial data if empty
    await SeedDataAsync(db, bootstrapStoreId);
    await SeedBootstrapAdminAsync(db, adminBootstrapSettings, adminPasswordHasher, logger, bootstrapStoreId);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

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
        var pla = new MaterialType { StoreId = storeId, Name = "PLA", BasePricePerKg = 150m, IsActive = true };
        var petg = new MaterialType { StoreId = storeId, Name = "PETG", BasePricePerKg = 180m, IsActive = true };
        var abs = new MaterialType { StoreId = storeId, Name = "ABS", BasePricePerKg = 200m, IsActive = true };
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

    // Seed Categories and Products (minimal but rich enough for UI/demo)
    if (!await db.Categories.AnyAsync(x => x.StoreId == storeId))
    {
        db.Categories.AddRange(
            new Category { StoreId = storeId, Name = "Toys", Slug = "toys", Description = "Playful prints", SortOrder = 1, IsActive = true },
            new Category { StoreId = storeId, Name = "Home", Slug = "home", Description = "Home helpers", SortOrder = 2, IsActive = true },
            new Category { StoreId = storeId, Name = "Tools", Slug = "tools", Description = "Workshop aids", SortOrder = 3, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    if (!await db.Products.AnyAsync(x => x.StoreId == storeId))
    {
        var catToys = await db.Categories.FirstAsync(c => c.StoreId == storeId && c.Slug == "toys");
        var catHome = await db.Categories.FirstAsync(c => c.StoreId == storeId && c.Slug == "home");
        var catTools = await db.Categories.FirstAsync(c => c.StoreId == storeId && c.Slug == "tools");

        var plaId = materials.First(m => m.Name == "PLA").Id;
        var petgId = materials.First(m => m.Name == "PETG").Id;
        var absId = materials.First(m => m.Name == "ABS").Id;
        var black = colors.First(c => c.Name == "Black").Id;
        var red = colors.First(c => c.Name == "Red").Id;
        var blue = colors.First(c => c.Name == "Blue").Id;
        var white = colors.First(c => c.Name == "White").Id;

        var products = new List<Product>
        {
            new()
            {
                StoreId = storeId,
                Title = "Robot Stand",
                Slug = "robot-stand",
                Description = "Display and charge your desktop robot with cable routing.",
                MainImageUrl = "https://placehold.co/800x600/1e1e2f/ffffff?text=Robot+Stand",
                IsActive = true,
                Categories = new List<Category> { catToys },
                Variants = new List<ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = plaId, ColorId = black, WidthMm = 80, HeightMm = 60, DepthMm = 40, WeightGrams = 220, PriceOffset = 8, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = plaId, ColorId = red, WidthMm = 100, HeightMm = 80, DepthMm = 50, WeightGrams = 320, PriceOffset = 11, IsActive = true }
                }
            },
            new()
            {
                StoreId = storeId,
                Title = "Mini Car",
                Slug = "mini-car",
                Description = "Snap-fit mini car for desk racing.",
                MainImageUrl = "https://placehold.co/800x600/1b3c59/ffffff?text=Mini+Car",
                IsActive = true,
                Categories = new List<Category> { catToys },
                Variants = new List<ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = petgId, ColorId = blue, WidthMm = 50, HeightMm = 30, DepthMm = 25, WeightGrams = 120, PriceOffset = 5, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = petgId, ColorId = red, WidthMm = 70, HeightMm = 40, DepthMm = 30, WeightGrams = 180, PriceOffset = 7, IsActive = true }
                }
            },
            new()
            {
                StoreId = storeId,
                Title = "Cable Clip Set",
                Slug = "cable-clip",
                Description = "Pack of cable clips to tidy your workspace.",
                MainImageUrl = "https://placehold.co/800x600/123524/ffffff?text=Cable+Clip",
                IsActive = true,
                Categories = new List<Category> { catHome },
                Variants = new List<ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = plaId, ColorId = white, WidthMm = 35, HeightMm = 15, DepthMm = 12, WeightGrams = 40, PriceOffset = 2, IsActive = true },
                    new() { SizeLabel = "M", MaterialTypeId = plaId, ColorId = black, WidthMm = 45, HeightMm = 18, DepthMm = 14, WeightGrams = 55, PriceOffset = 3, IsActive = true }
                }
            },
            new()
            {
                StoreId = storeId,
                Title = "Wall Hook",
                Slug = "wall-hook",
                Description = "Sturdy wall hook with hidden screw mount.",
                MainImageUrl = "https://placehold.co/800x600/4a2c2a/ffffff?text=Wall+Hook",
                IsActive = true,
                Categories = new List<Category> { catHome },
                Variants = new List<ProductVariant>
                {
                    new() { SizeLabel = "S", MaterialTypeId = petgId, ColorId = black, WidthMm = 60, HeightMm = 70, DepthMm = 20, WeightGrams = 90, PriceOffset = 4, IsActive = true }
                }
            },
            new()
            {
                StoreId = storeId,
                Title = "Wrench Holder",
                Slug = "wrench-holder",
                Description = "Wall-mount organizer for wrenches and small tools.",
                MainImageUrl = "https://placehold.co/800x600/2f2f2f/ffffff?text=Wrench+Holder",
                IsActive = true,
                Categories = new List<Category> { catTools },
                Variants = new List<ProductVariant>
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

async Task SeedBootstrapAdminAsync(
    AppDbContext db,
    AdminBootstrapSettings adminBootstrapSettings,
    IPasswordHasher<AdminUser> passwordHasher,
    ILogger logger,
    Guid storeId)
{
    if (await db.AdminUsers.AnyAsync(x => x.StoreId == storeId))
        return;

    var email = (adminBootstrapSettings.Email ?? string.Empty).Trim();
    var password = adminBootstrapSettings.Password ?? string.Empty;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning("Admin bootstrap skipped because AdminBootstrap:Email/Password are not configured and no admin users exist.");
        return;
    }

    var entity = new AdminUser
    {
        StoreId = storeId,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        IsActive = true
    };

    entity.PasswordHash = passwordHasher.HashPassword(entity, password);
    db.AdminUsers.Add(entity);
    await db.SaveChangesAsync();

    logger.LogInformation("Initial admin user bootstrap completed for {Email} in store {StoreId}.", email, storeId);
}

public partial class Program { }
