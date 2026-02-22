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
}