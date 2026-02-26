using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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

    private sealed record ColorListItem(Guid Id, string Name, string Hex);
}