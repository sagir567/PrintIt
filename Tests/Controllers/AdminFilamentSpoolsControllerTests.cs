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
        _client.LoginAsAdminAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Admin_filament_spools_should_return_unauthorized_without_auth_cookie()
    {
        _client.ClearAuthCookie();

        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(Guid.NewGuid(), 1000, null));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _client.LoginAsAdminAsync();
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

    [Fact]
    public async Task Consume_should_return_bad_request_when_grams_used_is_not_positive()
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
            db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000" });

            var filamentId = Guid.NewGuid();
            spoolId = Guid.NewGuid();

            db.Filaments.Add(new Filament
            {
                Id = filamentId,
                Brand = "ValidationTest",
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
        }

        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filament-spools/{spoolId}/consume",
            new ConsumeRequest(gramsUsed: 0)
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert (DB unchanged)
        await using var verifyScope = _api.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updated = await verifyDb.FilamentSpools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spoolId);

        updated.Should().NotBeNull();
        updated!.RemainingGrams.Should().Be(200);
        updated.Status.Should().Be("Opened");
    }

    [Fact]
    public async Task Consume_should_return_not_found_when_spool_does_not_exist()
    {
        // Act
        var resp = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/filament-spools/{Guid.NewGuid()}/consume",
            new ConsumeRequest(gramsUsed: 10)
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_initial_grams_not_positive()
    {
        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: Guid.NewGuid(),
                InitialGrams: 0,
                RemainingGrams: null
            )
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_remaining_grams_invalid()
    {
        // Arrange
        var filamentId = Guid.NewGuid();

        // Case 1: remaining <= 0
        var respNegative = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: filamentId,
                InitialGrams: 1000,
                RemainingGrams: 0
            )
        );

        respNegative.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Case 2: remaining > initial
        var respTooHigh = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: filamentId,
                InitialGrams: 1000,
                RemainingGrams: 1500
            )
        );

        respTooHigh.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_return_not_found_when_filament_missing()
    {
        // Arrange
        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);
        }

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: Guid.NewGuid(),
                InitialGrams: 1000,
                RemainingGrams: null
            )
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_should_return_conflict_when_filament_inactive()
    {
        Guid filamentId;

        // Arrange
        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;

            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA", IsActive = true });
            db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000", IsActive = true });

            filamentId = Guid.NewGuid();

            db.Filaments.Add(new Filament
            {
                Id = filamentId,
                Brand = "Inactive",
                MaterialTypeId = materialTypeId,
                ColorId = colorId,
                IsActive = false,
                CostPerKg = 100m,
                CreatedAtUtc = now
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: filamentId,
                InitialGrams: 1000,
                RemainingGrams: null
            )
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_should_create_spool_and_set_status_new_when_full()
    {
        Guid filamentId;

        // Arrange
        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;

            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA", IsActive = true });
            db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000", IsActive = true });

            filamentId = Guid.NewGuid();

            db.Filaments.Add(new Filament
            {
                Id = filamentId,
                Brand = "Prusa",
                MaterialTypeId = materialTypeId,
                ColorId = colorId,
                IsActive = true,
                CostPerKg = 120m,
                CreatedAtUtc = now
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: filamentId,
                InitialGrams: 1000,
                RemainingGrams: null
            )
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await resp.Content.ReadFromJsonAsync<CreatedSpoolResponse>();
        payload.Should().NotBeNull();
        payload!.FilamentId.Should().Be(filamentId);
        payload.InitialGrams.Should().Be(1000);
        payload.RemainingGrams.Should().Be(1000);
        payload.Status.Should().Be("New");
        payload.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Create_should_set_status_opened_when_remaining_less_than_initial()
    {
        Guid filamentId;

        // Arrange
        await using (var arrangeScope = _api.Services.CreateAsyncScope())
        {
            var db = arrangeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var now = DateTime.UtcNow;

            var materialTypeId = Guid.NewGuid();
            var colorId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialTypeId, Name = "PLA", IsActive = true });
            db.Colors.Add(new Color { Id = colorId, Name = "Black", Hex = "#000000", IsActive = true });

            filamentId = Guid.NewGuid();

            db.Filaments.Add(new Filament
            {
                Id = filamentId,
                Brand = "Prusa",
                MaterialTypeId = materialTypeId,
                ColorId = colorId,
                IsActive = true,
                CostPerKg = 120m,
                CreatedAtUtc = now
            });

            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/filament-spools",
            new CreateFilamentSpoolRequest(
                FilamentId: filamentId,
                InitialGrams: 1000,
                RemainingGrams: 500
            )
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await resp.Content.ReadFromJsonAsync<CreatedSpoolResponse>();
        payload.Should().NotBeNull();
        payload!.FilamentId.Should().Be(filamentId);
        payload.InitialGrams.Should().Be(1000);
        payload.RemainingGrams.Should().Be(500);
        payload.Status.Should().Be("Opened");
    }

    private static async Task ResetDbAsync(AppDbContext db)
    {
        // Clear tables in FK-safe order so tests are isolated and deterministic.
        db.ProductVariants.RemoveRange(db.ProductVariants);
        db.Products.RemoveRange(db.Products);
        db.Categories.RemoveRange(db.Categories);
        db.FilamentSpools.RemoveRange(db.FilamentSpools);
        db.Filaments.RemoveRange(db.Filaments);
        db.Colors.RemoveRange(db.Colors);
        db.MaterialTypes.RemoveRange(db.MaterialTypes);

        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ResetDbAsync(db).GetAwaiter().GetResult();
        }

        _client.Dispose();
        _api.Dispose();
    }

    // Request DTO for the consume endpoint.
    // If your endpoint expects a different property name, change it here.
    private sealed record ConsumeRequest(int gramsUsed);
    private sealed record CreateFilamentSpoolRequest(Guid FilamentId, int InitialGrams, int? RemainingGrams);

    // Response DTO for spool creation endpoint (only fields we assert on).
    private sealed record CreatedSpoolResponse(
        Guid Id,
        Guid FilamentId,
        int InitialGrams,
        int RemainingGrams,
        string Status,
        DateTime CreatedAtUtc,
        DateTime? LastUsedAtUtc
    );
}