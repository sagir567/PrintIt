using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Tests.Infrastructure;
using Xunit;

namespace PrintIt.Tests.Endpoints;

// Integration tests for the public filaments endpoint.
public sealed class FilamentsEndpointTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public FilamentsEndpointTests(PostgresFixture pg)
    {
        // Boot the API against the container DB.
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

    [Fact]
    public async Task Get_filaments_should_return_only_in_stock_items()
    {
        // Arrange: seed DB with two filaments, only one has RemainingGrams > 0.
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var materialTypeId = Guid.NewGuid();
        var colorId = Guid.NewGuid();

        // Seed lookup tables because the endpoint projects MaterialType.Name and Color.Name/Hex.
        var materialType = new MaterialType
        {
            Id = materialTypeId,
            Name = "PLA"
        };

        var color = new Color
        {
            Id = colorId,
            Name = "Black",
            Hex = "#000000"
        };

        var now = DateTime.UtcNow;

        var inStock = new Filament
        {
            Id = Guid.NewGuid(),
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
                    Id = Guid.NewGuid(),
                    InitialGrams = 1000,
                    RemainingGrams = 250,
                    Status = "Opened",
                    CreatedAtUtc = now
                }
            }
        };

        var outOfStock = new Filament
        {
            Id = Guid.NewGuid(),
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
                    Id = Guid.NewGuid(),
                    InitialGrams = 1000,
                    RemainingGrams = 0,
                    Status = "Empty",
                    CreatedAtUtc = now
                }
            }
        };

        db.AddRange(materialType, color, inStock, outOfStock);
        await db.SaveChangesAsync();

        // Act
        var resp = await _client.GetAsync("/api/v1/filaments");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadFromJsonAsync<List<FilamentListItem>>();
        json.Should().NotBeNull();

        json!.Select(x => x.Brand).Should().Contain("Prusa");
        json!.Select(x => x.Brand).Should().NotContain("Generic");
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    // Minimal DTO matching the endpoint response shape for the fields we assert on.
    private sealed record FilamentListItem(string Brand);
}
