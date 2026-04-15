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
                CostPerKg = 150.00m,
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
