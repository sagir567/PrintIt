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

// Integration tests for: GET /api/v1/admin/colors
public sealed class AdminColorsControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public AdminColorsControllerTests(PostgresFixture pg)
    {
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

    [Fact]
    public async Task Get_colors_should_return_all_colors_ordered_by_name()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await ResetDbAsync(db);

        // TODO(You): seed 3 colors with names that are NOT already sorted
        db.Colors.Add(new Color{ Id = Guid.NewGuid(), Name = "marble", Hex = "#ABCDEF" });
        db.Colors.Add(new Color{ Id = Guid.NewGuid(), Name = "midnight blue", Hex = "#191970" });
        db.Colors.Add(new Color{ Id = Guid.NewGuid(), Name = "oak", Hex = "#C19A6B" });
        await db.SaveChangesAsync();

        // Example: "White", "Black", "Red"
        // Make sure each has: Id, Name, Hex
        // db.Colors.AddRange(...);
        // await db.SaveChangesAsync();

        // Act
        var resp = await _client.GetAsync("/api/v1/admin/colors");

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadFromJsonAsync<List<ColorListItem>>();
        json.Should().NotBeNull();
        json!.Count.Should().Be(3);

        json.Select(x => x.Name).Should().ContainInOrder("marble", "midnight blue", "oak");
        
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_name_is_missing_or_whitespace()
    {
        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: "   ", Hex: "#000000")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_name_exceeds_max_length()
    {
        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: new string('x', 51), Hex: "#000000")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_allow_empty_hex_and_store_null()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: "Black", Hex: "")
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await resp.Content.ReadFromJsonAsync<ColorItem>();
        json.Should().NotBeNull();
        json!.Name.Should().Be("Black");
        json.Hex.Should().BeNull();
        json.IsActive.Should().BeTrue();

        // Assert (DB)
        var created = await db.Colors.AsNoTracking().SingleAsync(x => x.Id == json.Id);
        created.Hex.Should().BeNull();
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_hex_invalid_format()
    {
        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: "Black", Hex: "000000")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_trim_inputs()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: "  Black  ", Hex: "  #000000  ")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await resp.Content.ReadFromJsonAsync<ColorItem>();
        json.Should().NotBeNull();
        json!.Name.Should().Be("Black");
        json.Hex.Should().Be("#000000");
    }

    [Fact]
    public async Task Create_should_return_conflict_when_color_already_exists_and_active()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        db.Colors.Add(new Color { Name = "Black", Hex = "#000000", IsActive = true });
        await db.SaveChangesAsync();

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: "Black", Hex: "#000000")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_should_reactivate_existing_inactive_color_and_update_hex()
    {
        Guid id;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var entity = new Color { Name = "Black", Hex = "#111111", IsActive = false };
            db.Colors.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/colors",
            new CreateColorRequest(Name: "Black", Hex: "#000000")
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadFromJsonAsync<ColorItem>();
        json.Should().NotBeNull();
        json!.Id.Should().Be(id);
        json.IsActive.Should().BeTrue();
        json.Hex.Should().Be("#000000");

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Colors.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        updated.IsActive.Should().BeTrue();
        updated.Hex.Should().Be("#000000");
    }

    [Fact]
    public async Task Deactivate_should_return_not_found_when_missing()
    {
        // Act
        var resp = await _client.PatchAsync($"/api/v1/admin/colors/{Guid.NewGuid()}/deactivate", content: null);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_should_set_inactive_and_be_idempotent()
    {
        Guid id;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var entity = new Color { Name = "Black", Hex = "#000000", IsActive = true };
            db.Colors.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act 1
        var resp1 = await _client.PatchAsync($"/api/v1/admin/colors/{id}/deactivate", content: null);
        resp1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act 2 (idempotent)
        var resp2 = await _client.PatchAsync($"/api/v1/admin/colors/{id}/deactivate", content: null);
        resp2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Colors.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Activate_should_return_not_found_when_missing()
    {
        // Act
        var resp = await _client.PatchAsync($"/api/v1/admin/colors/{Guid.NewGuid()}/activate", content: null);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Activate_should_set_active_and_be_idempotent()
    {
        Guid id;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var entity = new Color { Name = "Black", Hex = "#000000", IsActive = false };
            db.Colors.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act 1
        var resp1 = await _client.PatchAsync($"/api/v1/admin/colors/{id}/activate", content: null);
        resp1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act 2 (idempotent)
        var resp2 = await _client.PatchAsync($"/api/v1/admin/colors/{id}/activate", content: null);
        resp2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Colors.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        updated.IsActive.Should().BeTrue();
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

    private sealed record ColorListItem(Guid Id, string Name, string Hex);
    private sealed record CreateColorRequest(string Name, string? Hex);
    private sealed record ColorItem(Guid Id, string Name, string? Hex, bool IsActive);
}