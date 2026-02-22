using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Tests.Infrastructure;
using Xunit;

namespace PrintIt.Tests.Endpoints;

// Integration tests for: PATCH /api/v1/admin/filament-spools/{id}/consume
public sealed class AdminFilamentSpoolsControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public AdminFilamentSpoolsControllerTests(PostgresFixture pg)
    {
        // Boot the API against the container DB.
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

    [Fact]
    public async Task Consume_should_decrease_remaining_and_update_status()
    {
        // Arrange
        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
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
                        RemainingGrams = 200,
                        Status = "Opened",
                        CreatedAtUtc = now,
                        LastUsedAtUtc = null
                    }
                }
            });

            await db.SaveChangesAsync();

            // Act
            var resp = await _client.PatchAsJsonAsync(
                $"/api/v1/admin/filament-spools/{spoolId}/consume",
                new ConsumeRequest(gramsUsed: 50)
            );

            // Assert (HTTP)
            resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Assert (DB) - use a fresh scope/context to avoid stale tracked entities
            await using var verifyScope = _api.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var updated = await verifyDb.FilamentSpools
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == spoolId);

            updated.Should().NotBeNull();
            updated!.RemainingGrams.Should().Be(150);
            updated.Status.Should().Be("Opened");
            updated.LastUsedAtUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Consume_should_mark_empty_when_remaining_reaches_zero()
    {
        // Arrange
        Guid spoolId;

        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;

            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
            db.Colors.Add(new Color { Id = colorId, Name = "White", Hex = "#FFFFFF" });

            var filamentId = Guid.NewGuid();
            spoolId = Guid.NewGuid();

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
                        Id = spoolId,
                        FilamentId = filamentId,
                        InitialGrams = 1000,
                        RemainingGrams = 30,
                        Status = "Opened",
                        CreatedAtUtc = now,
                        LastUsedAtUtc = null
                    }
                }
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filament-spools/{spoolId}/consume",
            new ConsumeRequest(gramsUsed: 30)
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB) - fresh scope/context
        await using var verifyScope = _api.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updated = await verifyDb.FilamentSpools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spoolId);

        updated.Should().NotBeNull();
        updated!.RemainingGrams.Should().Be(0);
        updated.Status.Should().Be("Empty");
        updated.LastUsedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_should_return_conflict_when_usage_exceeds_remaining()
    {
        // Arrange
        Guid spoolId;

        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;

            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA" });
            db.Colors.Add(new Color { Id = colorId, Name = "Red", Hex = "#FF0000" });

            var filamentId = Guid.NewGuid();
            spoolId = Guid.NewGuid();

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
                        CreatedAtUtc = now,
                        LastUsedAtUtc = null
                    }
                }
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filament-spools/{spoolId}/consume",
            new ConsumeRequest(gramsUsed: 999)
        );

        // Assert (HTTP) - matches current API behavior
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert (DB unchanged) - fresh scope/context
        await using var verifyScope = _api.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updated = await verifyDb.FilamentSpools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spoolId);

        updated.Should().NotBeNull();
        updated!.RemainingGrams.Should().Be(10);
        updated.Status.Should().Be("Opened");
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

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    // Request DTO for the consume endpoint.
    // If your endpoint expects a different property name, change it here.
    private sealed record ConsumeRequest(int gramsUsed);
}