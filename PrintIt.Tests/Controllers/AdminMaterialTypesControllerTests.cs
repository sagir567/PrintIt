using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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

    private sealed record MaterialTypeListItem(Guid Id, string Name);
}