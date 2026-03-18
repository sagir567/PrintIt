using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Tests.Infrastructure;
using Xunit;

namespace PrintIt.Tests.Controllers;

// Integration tests for: PATCH /api/v1/admin/filaments/{id}/consume
public sealed class AdminFilamentsControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public AdminFilamentsControllerTests(PostgresFixture pg)
    {
        // Boot the API against the container DB.
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

   [Fact]
public async Task Consume_should_use_smallest_sufficient_spool()
{
    Guid filamentId;
    Guid smallEnoughSpoolId;
    Guid bigEnoughSpoolId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        var now = DateTime.UtcNow;
        var materialTypeId = Guid.NewGuid();
        var colorId = Guid.NewGuid();

        filamentId = Guid.NewGuid();
        smallEnoughSpoolId = Guid.NewGuid();
        bigEnoughSpoolId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
        db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000" });

        db.Filaments.Add(new Filament
        {
            Id = filamentId,
            Brand = "Prusa",
            MaterialTypeId = materialTypeId,
            ColorId = colorId,
            IsActive = true,
            CostPerKg = 120m,
            CreatedAtUtc = now,
            Spools = new List<FilamentSpool>
            {
                new()
                {
                    Id = bigEnoughSpoolId,
                    FilamentId = filamentId,
                    InitialGrams = 1000,
                    RemainingGrams = 200,
                    Status = "Opened",
                    CreatedAtUtc = now.AddMinutes(-10)
                },
                new()
                {
                    Id = smallEnoughSpoolId,
                    FilamentId = filamentId,
                    InitialGrams = 1000,
                    RemainingGrams = 60,
                    Status = "New",
                    CreatedAtUtc = now.AddMinutes(-5)
                }
            }
        });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PatchAsJsonAsync(
        $"/api/v1/admin/filaments/{filamentId}/consume",
        new ConsumeRequest(gramsUsed: 50)
    );

    // Assert (HTTP)
    resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

    // Assert (DB) - read fresh in a new scope (no EF tracking)
    using (var verifyScope = _api.Services.CreateScope())
    {
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var small = await verifyDb.FilamentSpools.AsNoTracking()
            .SingleAsync(x => x.Id == smallEnoughSpoolId);

        var big = await verifyDb.FilamentSpools.AsNoTracking()
            .SingleAsync(x => x.Id == bigEnoughSpoolId);

        // Expectation: "smallest sufficient spool" is consumed
        small.RemainingGrams.Should().Be(10);
        small.LastUsedAtUtc.Should().NotBeNull();

        big.RemainingGrams.Should().Be(200);
        big.LastUsedAtUtc.Should().BeNull();
    }
}
    [Fact]
    public async Task Consume_should_skip_spools_that_cannot_cover_usage()
    {
        Guid filamentId;
        Guid insufficientSpoolId;
        Guid sufficientSpoolId;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;
            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            filamentId = Guid.NewGuid();
            insufficientSpoolId = Guid.NewGuid();
            sufficientSpoolId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
            db.Colors.Add(new Color { Id = colorId, Name = "White", Hex = "#FFFFFF" });

            db.Filaments.Add(new Filament
            {
                Id = filamentId,
                Brand = "Generic",
                MaterialTypeId = materialTypeId,
                ColorId = colorId,
                IsActive = true,
                CostPerKg = 90m,
                CreatedAtUtc = now,
                Spools = new List<FilamentSpool>
                {
                    new()
                    {
                        Id = insufficientSpoolId,
                        FilamentId = filamentId,
                        InitialGrams = 1000,
                        RemainingGrams = 20,
                        Status = "Opened",
                        CreatedAtUtc = now.AddMinutes(-10)
                    },
                    new()
                    {
                        Id = sufficientSpoolId,
                        FilamentId = filamentId,
                        InitialGrams = 1000,
                        RemainingGrams = 80,
                        Status = "Opened",
                        CreatedAtUtc = now.AddMinutes(-5)
                    }
                }
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filaments/{filamentId}/consume",
            new ConsumeRequest(gramsUsed: 50)
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB) - read fresh in a new scope (no EF tracking)
        using (var verifyScope = _api.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var insufficient = await verifyDb.FilamentSpools.AsNoTracking()
                .SingleAsync(x => x.Id == insufficientSpoolId);

            var sufficient = await verifyDb.FilamentSpools.AsNoTracking()
                .SingleAsync(x => x.Id == sufficientSpoolId);

            insufficient.RemainingGrams.Should().Be(20);
            insufficient.LastUsedAtUtc.Should().BeNull();

            sufficient.RemainingGrams.Should().Be(30);
            sufficient.LastUsedAtUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Consume_should_return_conflict_when_no_spool_can_cover_usage()
    {
        Guid filamentId;
        Guid spoolId;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;
            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            filamentId = Guid.NewGuid();
            spoolId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
            db.Colors.Add(new Color { Id = colorId, Name = "Red", Hex = "#FF0000" });

            db.Filaments.Add(new Filament
            {
                Id = filamentId,
                Brand = "TooMuchTest",
                MaterialTypeId = materialTypeId,
                ColorId = colorId,
                IsActive = true,
                CostPerKg = 100m,
                CreatedAtUtc = now,
                Spools = new List<FilamentSpool>
                {
                    new()
                    {
                        Id = spoolId,
                        FilamentId = filamentId,
                        InitialGrams = 1000,
                        RemainingGrams = 10,
                        Status = "Opened",
                        CreatedAtUtc = now
                    }
                }
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filaments/{filamentId}/consume",
            new ConsumeRequest(gramsUsed: 999)
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert (DB unchanged) - read fresh in a new scope (no EF tracking)
        using (var verifyScope = _api.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var updated = await verifyDb.FilamentSpools.AsNoTracking()
                .SingleAsync(x => x.Id == spoolId);

            updated.RemainingGrams.Should().Be(10);
            updated.LastUsedAtUtc.Should().BeNull();
        }
    }

    [Fact]
    public async Task Consume_should_return_bad_request_when_grams_used_is_not_positive()
    {
        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filaments/{Guid.NewGuid()}/consume",
            new ConsumeRequest(gramsUsed: 0)
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Consume_should_return_not_found_when_filament_does_not_exist()
    {
        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filaments/{Guid.NewGuid()}/consume",
            new ConsumeRequest(gramsUsed: 50)
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task ResetDbAsync(AppDbContext db)
    {
        // Clear tables in FK-safe order so tests are isolated and deterministic.
        db.FilamentSpools.RemoveRange(db.FilamentSpools);
        db.Filaments.RemoveRange(db.Filaments);
        db.Colors.RemoveRange(db.Colors);
        db.MaterialTypes.RemoveRange(db.MaterialTypes);

        await db.SaveChangesAsync();
    }

    private async Task<Guid> GetSingleFilamentIdAsync()
    {
        // Read fresh filament id from DB (avoids relying on local variables after scopes).
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Filaments.AsNoTracking().Select(x => x.Id).SingleAsync();
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    // Request DTO for the consume endpoint.
    private sealed record ConsumeRequest(int gramsUsed);
// ----------------------------
// GET SPOOLS
// ----------------------------

[Fact]
public async Task GetSpools_should_return_not_found_when_filament_does_not_exist()
{
    // Arrange
    using var scope = _api.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await ResetDbAsync(db);

    var missingFilamentId = Guid.NewGuid();

    // Act
    var resp = await _client.GetAsync($"/api/v1/admin/filaments/{missingFilamentId}/spools");

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task GetSpools_should_return_spools_ordered_by_created_desc()
{
    // Arrange
    using var scope = _api.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await ResetDbAsync(db);

    var now = DateTime.UtcNow;

    var materialTypeId = Guid.NewGuid();
    var colorId = Guid.NewGuid();

    db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
    db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000" });

    var filamentId = Guid.NewGuid();

    var olderSpoolId = Guid.NewGuid();
    var newerSpoolId = Guid.NewGuid();

    db.Filaments.Add(new Filament
    {
        Id = filamentId,
        Brand = "Prusa",
        MaterialTypeId = materialTypeId,
        ColorId = colorId,
        IsActive = true,
        CostPerKg = 120m,
        CreatedAtUtc = now,
        Spools = new List<FilamentSpool>
        {
            new()
            {
                Id = olderSpoolId,
                FilamentId = filamentId,
                InitialGrams = 1000,
                RemainingGrams = 800,
                Status = "Opened",
                CreatedAtUtc = now.AddDays(-2),
                LastUsedAtUtc = null
            },
            new()
            {
                Id = newerSpoolId,
                FilamentId = filamentId,
                InitialGrams = 1000,
                RemainingGrams = 1000,
                Status = "New",
                CreatedAtUtc = now.AddDays(-1),
                LastUsedAtUtc = null
            }
        }
    });

    await db.SaveChangesAsync();

    // Act
    var resp = await _client.GetAsync($"/api/v1/admin/filaments/{filamentId}/spools");

    // Assert (HTTP)
    resp.StatusCode.Should().Be(HttpStatusCode.OK);

    // Assert (payload order)
    var json = await resp.Content.ReadFromJsonAsync<List<SpoolListItem>>();
    json.Should().NotBeNull();
    json!.Count.Should().Be(2);

    // Newest first
    json[0].Id.Should().Be(newerSpoolId);
    json[1].Id.Should().Be(olderSpoolId);
}

// Minimal DTO matching the endpoint response fields we assert on.
private sealed record SpoolListItem(Guid Id, DateTime CreatedAtUtc);


[Fact]
public async Task GetSpools_should_return_expected_fields_and_values()
{
    // Arrange
    using var scope = _api.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await ResetDbAsync(db);

    var now = DateTime.UtcNow;

    var materialTypeId = Guid.NewGuid();
    var colorId = Guid.NewGuid();

    db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
    db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000" });

    var filamentId = Guid.NewGuid();
    var spoolId = Guid.NewGuid();

    db.Filaments.Add(new Filament
    {
        Id = filamentId,
        Brand = "Prusa",
        MaterialTypeId = materialTypeId,
        ColorId = colorId,
        IsActive = true,
        CostPerKg = 120m,
        CreatedAtUtc = now,
        Spools = new List<FilamentSpool>
        {
            new()
            {
                Id = spoolId,
                FilamentId = filamentId,
                InitialGrams = 1000,
                RemainingGrams = 321,
                Status = "Opened",
                CreatedAtUtc = now.AddHours(-3),
                LastUsedAtUtc = now.AddHours(-1)
            }
        }
    });

    await db.SaveChangesAsync();

    // Act
    var resp = await _client.GetAsync($"/api/v1/admin/filaments/{filamentId}/spools");

    // Assert (HTTP)
    resp.StatusCode.Should().Be(HttpStatusCode.OK);

    var json = await resp.Content.ReadFromJsonAsync<List<SpoolDetailsItem>>();
    json.Should().NotBeNull();
    json!.Count.Should().Be(1);

    var item = json[0];
    item.Id.Should().Be(spoolId);
    item.InitialGrams.Should().Be(1000);
    item.RemainingGrams.Should().Be(321);
    item.Status.Should().Be("Opened");
    item.CreatedAtUtc.Should().BeCloseTo(now.AddHours(-3), precision: TimeSpan.FromSeconds(2));
    item.LastUsedAtUtc.Should().NotBeNull();
}

private sealed record SpoolDetailsItem(
    Guid Id,
    int InitialGrams,
    int RemainingGrams,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc
);

// ----------------------------
// CREATE FILAMENT
// ----------------------------

[Fact]
public async Task Create_should_return_bad_request_when_brand_is_missing_or_whitespace()
{
    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: Guid.NewGuid(),
            ColorId: Guid.NewGuid(),
            Brand: "   ",
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Create_should_return_bad_request_when_brand_exceeds_max_length()
{
    var longBrand = new string('x', 81);

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: Guid.NewGuid(),
            ColorId: Guid.NewGuid(),
            Brand: longBrand,
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Create_should_return_bad_request_when_cost_not_positive()
{
    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: Guid.NewGuid(),
            ColorId: Guid.NewGuid(),
            Brand: "Prusa",
            CostPerKg: 0m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Create_should_return_not_found_when_material_type_missing()
{
    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: Guid.NewGuid(),
            ColorId: Guid.NewGuid(),
            Brand: "Prusa",
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Create_should_return_conflict_when_material_type_inactive()
{
    Guid materialTypeId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        materialTypeId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType
        {
            Id = materialTypeId,
            Name = "PLA",
            IsActive = false
        });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: materialTypeId,
            ColorId: Guid.NewGuid(),
            Brand: "Prusa",
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
public async Task Create_should_return_not_found_when_color_missing()
{
    Guid materialTypeId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        materialTypeId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType
        {
            Id = materialTypeId,
            Name = "PLA",
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: materialTypeId,
            ColorId: Guid.NewGuid(),
            Brand: "Prusa",
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Create_should_return_conflict_when_color_inactive()
{
    Guid materialTypeId;
    Guid colorId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        materialTypeId = Guid.NewGuid();
        colorId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType
        {
            Id = materialTypeId,
            Name = "PLA",
            IsActive = true
        });

        db.Colors.Add(new Color
        {
            Id = colorId,
            Name = "Black",
            Hex = "#000000",
            IsActive = false
        });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: materialTypeId,
            ColorId: colorId,
            Brand: "Prusa",
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
public async Task Create_should_return_conflict_when_filament_already_exists_and_active()
{
    Guid materialTypeId;
    Guid colorId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        var now = DateTime.UtcNow;

        materialTypeId = Guid.NewGuid();
        colorId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA", IsActive = true });
        db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000", IsActive = true });

        db.Filaments.Add(new Filament
        {
            Id = Guid.NewGuid(),
            Brand = "Prusa",
            MaterialTypeId = materialTypeId,
            ColorId = colorId,
            IsActive = true,
            CostPerKg = 90m,
            CreatedAtUtc = now
        });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: materialTypeId,
            ColorId: colorId,
            Brand: "Prusa",
            CostPerKg: 100m
        )
    );

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
public async Task Create_should_reactivate_existing_inactive_filament()
{
    Guid materialTypeId;
    Guid colorId;
    Guid filamentId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        var now = DateTime.UtcNow;

        materialTypeId = Guid.NewGuid();
        colorId = Guid.NewGuid();
        filamentId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA", IsActive = true });
        db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000", IsActive = true });

        db.Filaments.Add(new Filament
        {
            Id = filamentId,
            Brand = "Prusa",
            MaterialTypeId = materialTypeId,
            ColorId = colorId,
            IsActive = false,
            CostPerKg = 80m,
            CreatedAtUtc = now
        });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: materialTypeId,
            ColorId: colorId,
            Brand: "Prusa",
            CostPerKg: 120m
        )
    );

    // Assert (HTTP)
    resp.StatusCode.Should().Be(HttpStatusCode.OK);

    var json = await resp.Content.ReadFromJsonAsync<CreatedFilamentResponse>();
    json.Should().NotBeNull();
    json!.Id.Should().Be(filamentId);
    json.CostPerKg.Should().Be(120m);
    json.IsActive.Should().BeTrue();

    // Assert (DB)
    using (var verifyScope = _api.Services.CreateScope())
    {
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var filament = await verifyDb.Filaments.AsNoTracking().SingleAsync(x => x.Id == filamentId);
        filament.IsActive.Should().BeTrue();
        filament.CostPerKg.Should().Be(120m);
    }
}

[Fact]
public async Task Create_should_create_new_filament_when_not_exists()
{
    Guid materialTypeId;
    Guid colorId;

    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        var now = DateTime.UtcNow;

        materialTypeId = Guid.NewGuid();
        colorId = Guid.NewGuid();

        db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA", IsActive = true });
        db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000", IsActive = true });

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.PostAsJsonAsync(
        "/api/v1/admin/filaments",
        new CreateFilamentRequest(
            MaterialTypeId: materialTypeId,
            ColorId: colorId,
            Brand: "Prusa",
            CostPerKg: 120m
        )
    );

    // Assert (HTTP)
    resp.StatusCode.Should().Be(HttpStatusCode.Created);

    var json = await resp.Content.ReadFromJsonAsync<CreatedFilamentResponse>();
    json.Should().NotBeNull();
    json!.Brand.Should().Be("Prusa");
    json.MaterialTypeId.Should().Be(materialTypeId);
    json.ColorId.Should().Be(colorId);
    json.CostPerKg.Should().Be(120m);
    json.IsActive.Should().BeTrue();
}

// ----------------------------
// GET ALL FILAMENTS
// ----------------------------

[Fact]
public async Task GetAll_should_return_all_filaments_ordered_by_brand_material_and_color()
{
    // Arrange
    using (var scope = _api.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        var now = DateTime.UtcNow;

        var absId = Guid.NewGuid();
        var plaId = Guid.NewGuid();
        var blackId = Guid.NewGuid();
        var whiteId = Guid.NewGuid();

        db.MaterialTypes.AddRange(
            new MaterialType { Id = absId, Name = "ABS", IsActive = true },
            new MaterialType { Id = plaId, Name = "PLA", IsActive = true }
        );

        db.Colors.AddRange(
            new Color { Id = blackId, Name = "Black", Hex = "#000000", IsActive = true },
            new Color { Id = whiteId, Name = "White", Hex = "#FFFFFF", IsActive = true }
        );

        db.Filaments.AddRange(
            new Filament
            {
                Id = Guid.NewGuid(),
                Brand = "BrandX",
                MaterialTypeId = plaId,
                ColorId = whiteId,
                IsActive = true,
                CostPerKg = 120m,
                CreatedAtUtc = now
            },
            new Filament
            {
                Id = Guid.NewGuid(),
                Brand = "BrandX",
                MaterialTypeId = plaId,
                ColorId = blackId,
                IsActive = false,
                CostPerKg = 110m,
                CreatedAtUtc = now.AddMinutes(-1)
            },
            new Filament
            {
                Id = Guid.NewGuid(),
                Brand = "AnotherBrand",
                MaterialTypeId = absId,
                ColorId = blackId,
                IsActive = true,
                CostPerKg = 100m,
                CreatedAtUtc = now.AddMinutes(-2)
            }
        );

        await db.SaveChangesAsync();
    }

    // Act
    var resp = await _client.GetAsync("/api/v1/admin/filaments");

    // Assert (HTTP)
    resp.StatusCode.Should().Be(HttpStatusCode.OK);

    var json = await resp.Content.ReadFromJsonAsync<List<FilamentListItem>>();
    json.Should().NotBeNull();
    json!.Count.Should().Be(3);

    // Order by Brand, then MaterialType.Name, then Color.Name
    json.Select(x => (x.Brand, x.MaterialType.Name, x.Color.Name))
        .Should()
        .ContainInOrder(
            ("AnotherBrand", "ABS", "Black"),
            ("BrandX", "PLA", "Black"),
            ("BrandX", "PLA", "White")
        );
}

// Request DTOs / response DTOs used in tests
private sealed record CreateFilamentRequest(Guid MaterialTypeId, Guid ColorId, string Brand, decimal CostPerKg);

private sealed record CreatedFilamentResponse(
    Guid Id,
    Guid MaterialTypeId,
    Guid ColorId,
    string Brand,
    decimal CostPerKg,
    bool IsActive
);

private sealed record FilamentListItem(
    Guid Id,
    string Brand,
    decimal CostPerKg,
    bool IsActive,
    DateTime CreatedAtUtc,
    MaterialTypeSummary MaterialType,
    ColorSummary Color
);

private sealed record MaterialTypeSummary(Guid MaterialTypeId, string Name, bool IsActive);

private sealed record ColorSummary(Guid ColorId, string Name, string Hex, bool IsActive);

}