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

// Integration tests for: GET /api/v1/admin/material-types
public sealed class AdminMaterialTypesControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public AdminMaterialTypesControllerTests(PostgresFixture pg)
    {
        // Boot the API against the container DB.
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

    [Fact]
    public async Task Get_material_types_should_return_all_material_types_ordered_by_name()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await ResetDbAsync(db);

        // TODO(You): seed 3 material types with names that are NOT already sorted.
        db.MaterialTypes.Add(new MaterialType { Name = "PETG", IsActive = true });
        db.MaterialTypes.Add(new MaterialType { Name = "ABS", IsActive = true });
        db.MaterialTypes.Add(new MaterialType { Name = "PLA", IsActive = true });
        await db.SaveChangesAsync();

        // Example: "PETG", "ABS", "PLA"
        // Make sure each has: Id, Name
        // db.MaterialTypes.AddRange(...);
        // await db.SaveChangesAsync();

        // Act
        var resp = await _client.GetAsync("/api/v1/admin/material-types");

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadFromJsonAsync<List<MaterialTypeListItem>>();
        json.Should().NotBeNull();

        // TODO(You): assert that we got 3 items and they are ordered by Name ascending.
        // json!.Count.Should().Be(3);
        // json.Select(x => x.Name).Should().ContainInOrder("ABS", "PETG", "PLA");
        json!.Count.Should().Be(3);
        json.Select(x => x.Name).Should().ContainInOrder("ABS", "PETG", "PLA");
        json.Select(x => x.Name)
        .Should()
        .BeEquivalentTo(new[] { "ABS", "PETG", "PLA" });
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_name_is_missing_or_whitespace()
    {
        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/material-types",
            new CreateMaterialTypeRequest(Name: "   ")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_return_bad_request_when_name_exceeds_max_length()
    {
        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/material-types",
            new CreateMaterialTypeRequest(Name: new string('x', 51))
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_should_create_new_material_type_and_trim_name()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/material-types",
            new CreateMaterialTypeRequest(Name: "  PLA  ")
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await resp.Content.ReadFromJsonAsync<MaterialTypeItem>();
        json.Should().NotBeNull();
        json!.Name.Should().Be("PLA");
        json.IsActive.Should().BeTrue();

        // Assert (DB)
        var created = await db.MaterialTypes.AsNoTracking().SingleAsync(x => x.Id == json.Id);
        created.Name.Should().Be("PLA");
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_should_return_conflict_when_material_type_already_exists_and_active()
    {
        // Arrange
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ResetDbAsync(db);

        db.MaterialTypes.Add(new MaterialType { Name = "PLA", IsActive = true });
        await db.SaveChangesAsync();

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/material-types",
            new CreateMaterialTypeRequest(Name: "PLA")
        );

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_should_reactivate_existing_inactive_material_type()
    {
        Guid id;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var entity = new MaterialType { Name = "PLA", IsActive = false };
            db.MaterialTypes.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/material-types",
            new CreateMaterialTypeRequest(Name: "PLA")
        );

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadFromJsonAsync<MaterialTypeItem>();
        json.Should().NotBeNull();
        json!.Id.Should().Be(id);
        json.IsActive.Should().BeTrue();

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.MaterialTypes.AsNoTracking().SingleAsync(x => x.Id == id);
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_should_return_not_found_when_missing()
    {
        // Act
        var resp = await _client.PatchAsync(
            $"/api/v1/admin/material-types/{Guid.NewGuid()}/deactivate",
            content: null
        );

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

            var entity = new MaterialType { Name = "PLA", IsActive = true };
            db.MaterialTypes.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act 1
        var resp1 = await _client.PatchAsync($"/api/v1/admin/material-types/{id}/deactivate", content: null);
        resp1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act 2 (idempotent)
        var resp2 = await _client.PatchAsync($"/api/v1/admin/material-types/{id}/deactivate", content: null);
        resp2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.MaterialTypes.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Activate_should_return_not_found_when_missing()
    {
        // Act
        var resp = await _client.PatchAsync(
            $"/api/v1/admin/material-types/{Guid.NewGuid()}/activate",
            content: null
        );

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

            var entity = new MaterialType { Name = "PLA", IsActive = false };
            db.MaterialTypes.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act 1
        var resp1 = await _client.PatchAsync($"/api/v1/admin/material-types/{id}/activate", content: null);
        resp1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act 2 (idempotent)
        var resp2 = await _client.PatchAsync($"/api/v1/admin/material-types/{id}/activate", content: null);
        resp2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.MaterialTypes.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_should_return_not_found_when_missing()
    {
        // Act
        var resp = await _client.DeleteAsync($"/api/v1/admin/material-types/{Guid.NewGuid()}");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_should_remove_row_from_database()
    {
        Guid id;

        // Arrange
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var entity = new MaterialType { Name = "PLA", IsActive = true };
            db.MaterialTypes.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        // Act
        var resp = await _client.DeleteAsync($"/api/v1/admin/material-types/{id}");

        // Assert (HTTP)
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (DB)
        using var verifyScope = _api.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await verifyDb.MaterialTypes.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.Id == id);
        exists.Should().BeFalse();
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

    private sealed record MaterialTypeListItem(Guid Id, string Name);
    private sealed record CreateMaterialTypeRequest(string Name);
    private sealed record MaterialTypeItem(Guid Id, string Name, bool IsActive);
}