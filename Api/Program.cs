using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using PrintIt.Api;
using PrintIt.Api.Auth;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = BuildJwtSettings(builder.Configuration);
var authCookieName = BuildAuthCookieName(builder.Configuration);

builder.Services.Configure<JwtSettings>(options =>
{
    options.Issuer = jwtSettings.Issuer;
    options.Audience = jwtSettings.Audience;
    options.SigningKey = jwtSettings.SigningKey;
    options.AccessTokenMinutes = jwtSettings.AccessTokenMinutes;
});
builder.Services.Configure<AuthCookieSettings>(builder.Configuration.GetSection(AuthCookieSettings.SectionName));
builder.Services.Configure<AdminBootstrapSettings>(builder.Configuration.GetSection(AdminBootstrapSettings.SectionName));
builder.Services.Configure<PrintIt.Api.StoreBootstrapSettings>(builder.Configuration.GetSection(PrintIt.Api.StoreBootstrapSettings.SectionName));

// Controllers (instead of the template Minimal API endpoints)
builder.Services.AddControllers();

// Add CORS for development
// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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

builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token) &&
                    context.Request.Cookies.TryGetValue(authCookieName, out var cookieToken))
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
        policy.RequireClaim(AdminStoreContext.StoreIdClaimType);
    });
});

var app = builder.Build();

// Ensure database schema is created / migrated on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var storeBootstrapSettings = scope.ServiceProvider.GetRequiredService<IOptions<PrintIt.Api.StoreBootstrapSettings>>().Value;
    var adminBootstrapSettings = scope.ServiceProvider.GetRequiredService<IOptions<AdminBootstrapSettings>>().Value;
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StoreBootstrap");

    db.Database.Migrate();

    var bootstrapStoreId = await SeedBootstrapStoreAsync(db, storeBootstrapSettings, logger);
    await SeedBootstrapAdminAsync(db, bootstrapStoreId, adminBootstrapSettings, passwordHasher, logger);

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

app.UseAuthentication();
app.UseAuthorization();

// Map controller routes
app.MapControllers();

app.Run();

async Task<Guid> SeedBootstrapStoreAsync(AppDbContext db, PrintIt.Api.StoreBootstrapSettings settings, ILogger logger)
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
    var requiredMaterials = new[]
    {
        new { Name = "PLA", BasePricePerKg = 105m },
        new { Name = "PETG", BasePricePerKg = 125m },
        new { Name = "ABS", BasePricePerKg = 135m }
    };

    var requiredColors = new[]
    {
        new { Name = "Black", Hex = "#000000" },
        new { Name = "White", Hex = "#FFFFFF" },
        new { Name = "Red", Hex = "#FF0000" }
    };

    var requiredFilaments = new[]
    {
        new { Material = "PLA", Color = "Black", Brand = "PrintaPro", CostPerKg = 82m },
        new { Material = "PLA", Color = "White", Brand = "PrintaPro", CostPerKg = 84m },
        new { Material = "PLA", Color = "Red", Brand = "PrintaPro", CostPerKg = 86m },
        new { Material = "PETG", Color = "Black", Brand = "LayerLine", CostPerKg = 94m },
        new { Material = "PETG", Color = "White", Brand = "LayerLine", CostPerKg = 96m },
        new { Material = "PETG", Color = "Red", Brand = "LayerLine", CostPerKg = 98m },
        new { Material = "ABS", Color = "Black", Brand = "ForgeFil", CostPerKg = 102m },
        new { Material = "ABS", Color = "White", Brand = "ForgeFil", CostPerKg = 104m },
        new { Material = "ABS", Color = "Red", Brand = "ForgeFil", CostPerKg = 106m }
    };

    var requiredCategories = new[]
    {
        new { Name = "Desk", Slug = "desk", Description = "Workspace and desk organization prints.", SortOrder = 1 },
        new { Name = "Home", Slug = "home", Description = "Functional home utility prints.", SortOrder = 2 },
        new { Name = "Gaming", Slug = "gaming", Description = "Accessories for gamers and controllers.", SortOrder = 3 },
        new { Name = "Accessories", Slug = "accessories", Description = "Everyday carry and accessory helpers.", SortOrder = 4 }
    };

    var requiredProducts = new[]
    {
        new
        {
            Title = "Adjustable Phone Stand",
            Slug = "adjustable-phone-stand",
            Description = "Stable desk phone stand with viewing angle support.",
            Image = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9",
            Categories = new[] { "desk", "accessories" },
            Variants = new[]
            {
                new { Size = "Standard", Material = "PLA", Color = "Black", W = 80, H = 110, D = 95, Weight = 95, Offset = 9m },
                new { Size = "Standard", Material = "PETG", Color = "White", W = 80, H = 110, D = 95, Weight = 102, Offset = 11m }
            }
        },
        new
        {
            Title = "Snap Cable Organizer",
            Slug = "snap-cable-organizer",
            Description = "Clip-style cable organizer for tidy desks.",
            Image = "https://images.unsplash.com/photo-1519389950473-47ba0277781c",
            Categories = new[] { "desk", "home" },
            Variants = new[]
            {
                new { Size = "3-Slot", Material = "PLA", Color = "White", W = 95, H = 18, D = 20, Weight = 32, Offset = 5m },
                new { Size = "5-Slot", Material = "PETG", Color = "Black", W = 145, H = 18, D = 20, Weight = 45, Offset = 7m }
            }
        },
        new
        {
            Title = "Minimal Wall Hook",
            Slug = "minimal-wall-hook",
            Description = "Compact wall hook for lightweight items.",
            Image = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85",
            Categories = new[] { "home" },
            Variants = new[]
            {
                new { Size = "Small", Material = "PETG", Color = "White", W = 30, H = 45, D = 35, Weight = 22, Offset = 4m },
                new { Size = "Large", Material = "ABS", Color = "Black", W = 45, H = 60, D = 45, Weight = 38, Offset = 6m }
            }
        },
        new
        {
            Title = "Dual Controller Stand",
            Slug = "dual-controller-stand",
            Description = "Desk stand for two game controllers.",
            Image = "https://images.unsplash.com/photo-1486401899868-0e435ed85128",
            Categories = new[] { "gaming", "desk" },
            Variants = new[]
            {
                new { Size = "Dual", Material = "PLA", Color = "Red", W = 160, H = 135, D = 120, Weight = 185, Offset = 14m },
                new { Size = "Dual", Material = "PETG", Color = "Black", W = 160, H = 135, D = 120, Weight = 195, Offset = 16m }
            }
        },
        new
        {
            Title = "Self-Watering Planter",
            Slug = "self-watering-planter",
            Description = "Compact planter with integrated water reservoir.",
            Image = "https://images.unsplash.com/photo-1463320726281-696a485928c7",
            Categories = new[] { "home" },
            Variants = new[]
            {
                new { Size = "Medium", Material = "PETG", Color = "White", W = 120, H = 110, D = 120, Weight = 165, Offset = 12m },
                new { Size = "Medium", Material = "ABS", Color = "Red", W = 120, H = 110, D = 120, Weight = 175, Offset = 13m }
            }
        },
        new
        {
            Title = "Under-Desk Headphone Hanger",
            Slug = "under-desk-headphone-hanger",
            Description = "Screw-mount hanger to keep headphones off the desk.",
            Image = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e",
            Categories = new[] { "desk", "accessories" },
            Variants = new[]
            {
                new { Size = "Standard", Material = "ABS", Color = "Black", W = 40, H = 55, D = 80, Weight = 48, Offset = 6m },
                new { Size = "Standard", Material = "PLA", Color = "White", W = 40, H = 55, D = 80, Weight = 44, Offset = 5m }
            }
        },
        new
        {
            Title = "Wall Key Holder",
            Slug = "wall-key-holder",
            Description = "Wall-mounted key rack with six hooks.",
            Image = "https://images.unsplash.com/photo-1484154218962-a197022b5858",
            Categories = new[] { "home", "accessories" },
            Variants = new[]
            {
                new { Size = "6-Hook", Material = "PLA", Color = "Black", W = 170, H = 55, D = 22, Weight = 68, Offset = 8m },
                new { Size = "6-Hook", Material = "PETG", Color = "Red", W = 170, H = 55, D = 22, Weight = 75, Offset = 10m }
            }
        },
        new
        {
            Title = "Desktop Utility Tray",
            Slug = "desktop-utility-tray",
            Description = "Everyday carry tray for wallet, keys, and watch.",
            Image = "https://images.unsplash.com/photo-1498050108023-c5249f4df085",
            Categories = new[] { "desk" },
            Variants = new[]
            {
                new { Size = "Small", Material = "PLA", Color = "White", W = 160, H = 25, D = 110, Weight = 88, Offset = 8m },
                new { Size = "Large", Material = "PETG", Color = "Black", W = 210, H = 28, D = 140, Weight = 122, Offset = 11m }
            }
        },
        new
        {
            Title = "Foldable Laptop Riser",
            Slug = "foldable-laptop-riser",
            Description = "Portable laptop stand for improved ergonomics.",
            Image = "https://images.unsplash.com/photo-1517336714739-489689fd1ca8",
            Categories = new[] { "desk", "accessories" },
            Variants = new[]
            {
                new { Size = "13-14 inch", Material = "ABS", Color = "Black", W = 240, H = 90, D = 215, Weight = 210, Offset = 16m },
                new { Size = "15-16 inch", Material = "PETG", Color = "Red", W = 270, H = 95, D = 230, Weight = 245, Offset = 19m }
            }
        },
        new
        {
            Title = "Hex Coaster Set",
            Slug = "hex-coaster-set",
            Description = "Set of interlocking hexagon drink coasters.",
            Image = "https://images.unsplash.com/photo-1517705008128-361805f42e86",
            Categories = new[] { "home", "accessories" },
            Variants = new[]
            {
                new { Size = "Set of 4", Material = "PLA", Color = "Red", W = 95, H = 6, D = 110, Weight = 54, Offset = 7m },
                new { Size = "Set of 6", Material = "PETG", Color = "White", W = 95, H = 8, D = 110, Weight = 78, Offset = 9m }
            }
        }
    };

    var materialByName = await db.MaterialTypes
        .Where(x => x.StoreId == storeId)
        .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

    foreach (var seed in requiredMaterials)
    {
        if (!materialByName.TryGetValue(seed.Name, out var entity))
        {
            entity = new MaterialType
            {
                StoreId = storeId,
                Name = seed.Name,
                BasePricePerKg = seed.BasePricePerKg,
                IsActive = true
            };
            db.MaterialTypes.Add(entity);
            materialByName[seed.Name] = entity;
            continue;
        }

        entity.BasePricePerKg = seed.BasePricePerKg;
        entity.IsActive = true;
    }
    await db.SaveChangesAsync();

    var colorByName = await db.Colors
        .Where(x => x.StoreId == storeId)
        .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

    foreach (var seed in requiredColors)
    {
        if (!colorByName.TryGetValue(seed.Name, out var entity))
        {
            entity = new Color
            {
                StoreId = storeId,
                Name = seed.Name,
                Hex = seed.Hex,
                IsActive = true
            };
            db.Colors.Add(entity);
            colorByName[seed.Name] = entity;
            continue;
        }

        entity.Hex = seed.Hex;
        entity.IsActive = true;
    }
    await db.SaveChangesAsync();

    foreach (var seed in requiredFilaments)
    {
        var materialId = materialByName[seed.Material].Id;
        var colorId = colorByName[seed.Color].Id;

        var filament = await db.Filaments.FirstOrDefaultAsync(x =>
            x.StoreId == storeId &&
            x.MaterialTypeId == materialId &&
            x.ColorId == colorId &&
            x.Brand == seed.Brand);

        if (filament is null)
        {
            filament = new Filament
            {
                StoreId = storeId,
                MaterialTypeId = materialId,
                ColorId = colorId,
                Brand = seed.Brand,
                CostPerKg = seed.CostPerKg,
                IsActive = true
            };
            db.Filaments.Add(filament);
        }
        else
        {
            filament.CostPerKg = seed.CostPerKg;
            filament.IsActive = true;
        }
    }
    await db.SaveChangesAsync();

    var requiredFilamentKeySet = requiredFilaments
        .Select(x => $"{x.Material}|{x.Color}|{x.Brand}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var filamentsForSpools = (await db.Filaments
        .Where(x => x.StoreId == storeId)
        .Include(x => x.MaterialType)
        .Include(x => x.Color)
        .ToListAsync())
        .Where(x => requiredFilamentKeySet.Contains($"{x.MaterialType.Name}|{x.Color.Name}|{x.Brand}"))
        .ToList();

    foreach (var filament in filamentsForSpools)
    {
        var hasNew = await db.FilamentSpools.AnyAsync(x => x.FilamentId == filament.Id && x.Status == "New");
        if (!hasNew)
        {
            db.FilamentSpools.Add(new FilamentSpool
            {
                FilamentId = filament.Id,
                InitialGrams = 1000,
                RemainingGrams = 1000,
                Status = "New"
            });
        }

        var hasOpened = await db.FilamentSpools.AnyAsync(x => x.FilamentId == filament.Id && x.Status == "Opened");
        if (!hasOpened)
        {
            db.FilamentSpools.Add(new FilamentSpool
            {
                FilamentId = filament.Id,
                InitialGrams = 1000,
                RemainingGrams = 700,
                Status = "Opened"
            });
        }
    }
    await db.SaveChangesAsync();

    var categoriesBySlug = await db.Categories
        .IgnoreQueryFilters()
        .ToDictionaryAsync(x => x.Slug, StringComparer.OrdinalIgnoreCase);

    foreach (var seed in requiredCategories)
    {
        if (!categoriesBySlug.TryGetValue(seed.Slug, out var category))
        {
            category = new Category
            {
                StoreId = storeId,
                Name = seed.Name,
                Slug = seed.Slug,
                Description = seed.Description,
                SortOrder = seed.SortOrder,
                IsActive = true
            };
            db.Categories.Add(category);
            categoriesBySlug[seed.Slug] = category;
            continue;
        }

        category.StoreId = storeId;
        category.Name = seed.Name;
        category.Description = seed.Description;
        category.SortOrder = seed.SortOrder;
        category.IsActive = true;
    }
    await db.SaveChangesAsync();

    foreach (var seed in requiredProducts)
    {
        var product = await db.Products
            .Include(x => x.Categories)
            .Include(x => x.Variants)
            .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Slug == seed.Slug);

        if (product is null)
        {
            product = new Product
            {
                StoreId = storeId,
                Title = seed.Title,
                Slug = seed.Slug,
                Description = seed.Description,
                MainImageUrl = seed.Image,
                IsActive = true
            };
            db.Products.Add(product);
        }
        else
        {
            product.Title = seed.Title;
            product.Description = seed.Description;
            product.MainImageUrl = seed.Image;
            product.IsActive = true;
        }

        product.Categories.Clear();
        foreach (var categorySlug in seed.Categories)
        {
            product.Categories.Add(categoriesBySlug[categorySlug]);
        }

        foreach (var variantSeed in seed.Variants)
        {
            var materialId = materialByName[variantSeed.Material].Id;
            var colorId = colorByName[variantSeed.Color].Id;

            var variant = product.Variants.FirstOrDefault(v =>
                string.Equals(v.SizeLabel, variantSeed.Size, StringComparison.Ordinal) &&
                v.MaterialTypeId == materialId &&
                v.ColorId == colorId);

            if (variant is null)
            {
                product.Variants.Add(new ProductVariant
                {
                    SizeLabel = variantSeed.Size,
                    MaterialTypeId = materialId,
                    ColorId = colorId,
                    WidthMm = variantSeed.W,
                    HeightMm = variantSeed.H,
                    DepthMm = variantSeed.D,
                    WeightGrams = variantSeed.Weight,
                    PriceOffset = variantSeed.Offset,
                    IsActive = true
                });
                continue;
            }

            variant.WidthMm = variantSeed.W;
            variant.HeightMm = variantSeed.H;
            variant.DepthMm = variantSeed.D;
            variant.WeightGrams = variantSeed.Weight;
            variant.PriceOffset = variantSeed.Offset;
            variant.IsActive = true;
        }
    }

    await db.SaveChangesAsync();
}

async Task SeedBootstrapAdminAsync(
    AppDbContext db,
    Guid storeId,
    AdminBootstrapSettings settings,
    IPasswordHasher<AdminUser> passwordHasher,
    ILogger logger)
{
    var email = (settings.Email ?? string.Empty).Trim();
    var password = settings.Password ?? string.Empty;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogInformation("Admin bootstrap skipped because email/password are not configured.");
        return;
    }

    var normalizedEmail = email.ToUpperInvariant();
    var admin = await db.AdminUsers.FirstOrDefaultAsync(x => x.StoreId == storeId && x.NormalizedEmail == normalizedEmail);
    if (admin is null)
    {
        admin = new AdminUser
        {
            StoreId = storeId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            IsActive = true
        };

        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Bootstrap admin created for store {StoreId}.", storeId);
        return;
    }

    var changed = false;
    if (!string.Equals(admin.Email, email, StringComparison.Ordinal))
    {
        admin.Email = email;
        changed = true;
    }

    if (!admin.IsActive)
    {
        admin.IsActive = true;
        changed = true;
    }

    if (passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
    {
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        changed = true;
    }

    if (changed)
    {
        await db.SaveChangesAsync();
    }
}

static JwtSettings BuildJwtSettings(IConfiguration configuration)
{
    var settings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

    if (string.IsNullOrWhiteSpace(settings.Issuer))
        settings.Issuer = "PrintIt.Api";

    if (string.IsNullOrWhiteSpace(settings.Audience))
        settings.Audience = "PrintIt.Admin";

    if (string.IsNullOrWhiteSpace(settings.SigningKey))
        settings.SigningKey = "DEV_ONLY_CHANGE_ME_12345678901234567890";

    if (settings.AccessTokenMinutes <= 0)
        settings.AccessTokenMinutes = 60;

    return settings;
}

static string BuildAuthCookieName(IConfiguration configuration)
{
    var configured = configuration[$"{AuthCookieSettings.SectionName}:Name"];
    return string.IsNullOrWhiteSpace(configured) ? "printit_admin_auth" : configured;
}

public partial class Program { }
