using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Tests.Infrastructure;

namespace PrintIt.Tests.Controllers;

public sealed class CatalogControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public CatalogControllerTests(PostgresFixture pg)
    {
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

    [Fact]
    public async Task Catalog_products_should_support_category_and_sorting()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var material = new MaterialType { Id = Guid.NewGuid(), Name = "PLA", BasePricePerKg = 200m, IsActive = true };
            var color = new Color { Id = Guid.NewGuid(), Name = "Black", Hex = "#000000", IsActive = true };
            var toys = new Category { Id = Guid.NewGuid(), Name = "Toys", Slug = "toys", IsActive = true, SortOrder = 1 };

            var robot = new Product
            {
                Id = Guid.NewGuid(),
                Title = "Robot",
                Slug = "robot",
                IsActive = true,
                Categories = new List<Category> { toys },
                Variants = new List<ProductVariant>
                {
                    new()
                    {
                        SizeLabel = "M",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 50,
                        HeightMm = 50,
                        DepthMm = 50,
                        WeightGrams = 200,
                        PriceOffset = 5,
                        IsActive = true
                    }
                }
            };

            var smallToy = new Product
            {
                Id = Guid.NewGuid(),
                Title = "Mini Toy",
                Slug = "mini-toy",
                IsActive = true,
                Categories = new List<Category> { toys },
                Variants = new List<ProductVariant>
                {
                    new()
                    {
                        SizeLabel = "S",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 20,
                        HeightMm = 20,
                        DepthMm = 20,
                        WeightGrams = 50,
                        PriceOffset = 2,
                        IsActive = true
                    }
                }
            };

            db.AddRange(material, color, toys, robot, smallToy);
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync("/api/v1/catalog/products?category=toys&sort=price_desc");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<CatalogProductsResponse>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(2);
        body.Items.Select(x => x.Slug).Should().ContainInOrder("robot", "mini-toy");
    }

    [Fact]
    public async Task Admin_products_should_support_soft_and_hard_delete()
    {
        Guid materialId;
        Guid colorId;
        Guid categoryId;

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            materialId = Guid.NewGuid();
            colorId = Guid.NewGuid();
            categoryId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType { Id = materialId, Name = "PLA", BasePricePerKg = 150m, IsActive = true });
            db.Colors.Add(new Color { Id = colorId, Name = "White", Hex = "#FFFFFF", IsActive = true });
            db.Categories.Add(new Category { Id = categoryId, Name = "Home", Slug = "home", IsActive = true });
            await db.SaveChangesAsync();
        }

        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/products", new
        {
            title = "Shelf Bracket",
            slug = "shelf-bracket",
            description = "Strong bracket",
            mainImageUrl = "",
            isActive = true,
            categoryIds = new[] { categoryId },
            variants = new[]
            {
                new
                {
                    sizeLabel = "Standard",
                    materialTypeId = materialId,
                    colorId = colorId,
                    widthMm = 60,
                    heightMm = 80,
                    depthMm = 30,
                    weightGrams = 120,
                    priceOffset = 10,
                    isActive = true
                }
            }
        });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedProductResponse>();
        created.Should().NotBeNull();

        var deactivateResp = await _client.PatchAsync($"/api/v1/admin/products/{created!.Id}/deactivate", null);
        deactivateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == created.Id);
            product.IsActive.Should().BeFalse();
        }

        var deleteResp = await _client.DeleteAsync($"/api/v1/admin/products/{created.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var exists = await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Id == created.Id);
            exists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Catalog_products_should_exclude_inactive_products_and_return_empty_for_unknown_category()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var material = new MaterialType { Id = Guid.NewGuid(), Name = "PETG", BasePricePerKg = 180m, IsActive = true };
            var color = new Color { Id = Guid.NewGuid(), Name = "Gray", Hex = "#808080", IsActive = true };
            var useful = new Category { Id = Guid.NewGuid(), Name = "Useful", Slug = "useful", IsActive = true, SortOrder = 1 };

            var activeProduct = new Product
            {
                Id = Guid.NewGuid(),
                Title = "Active Box",
                Slug = "active-box",
                IsActive = true,
                Categories = new List<Category> { useful },
                Variants = new List<ProductVariant>
                {
                    new()
                    {
                        SizeLabel = "M",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 40,
                        HeightMm = 40,
                        DepthMm = 40,
                        WeightGrams = 100,
                        PriceOffset = 3,
                        IsActive = true
                    }
                }
            };

            var inactiveProduct = new Product
            {
                Id = Guid.NewGuid(),
                Title = "Inactive Box",
                Slug = "inactive-box",
                IsActive = false,
                Categories = new List<Category> { useful },
                Variants = new List<ProductVariant>
                {
                    new()
                    {
                        SizeLabel = "M",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 40,
                        HeightMm = 40,
                        DepthMm = 40,
                        WeightGrams = 100,
                        PriceOffset = 3,
                        IsActive = true
                    }
                }
            };

            db.AddRange(material, color, useful, activeProduct, inactiveProduct);
            await db.SaveChangesAsync();
        }

        var unknownCategoryResp = await _client.GetAsync("/api/v1/catalog/products?category=does-not-exist");
        unknownCategoryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var unknownBody = await unknownCategoryResp.Content.ReadFromJsonAsync<CatalogProductsResponse>();
        unknownBody.Should().NotBeNull();
        unknownBody!.Total.Should().Be(0);
        unknownBody.Items.Should().BeEmpty();

        var usefulResp = await _client.GetAsync("/api/v1/catalog/products?category=useful");
        usefulResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var usefulBody = await usefulResp.Content.ReadFromJsonAsync<CatalogProductsResponse>();
        usefulBody.Should().NotBeNull();
        usefulBody!.Total.Should().Be(1);
        usefulBody.Items.Single().Slug.Should().Be("active-box");
    }

    [Fact]
    public async Task Catalog_products_should_calculate_price_from_lowest_active_variant()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            var material = new MaterialType { Id = Guid.NewGuid(), Name = "Nylon", BasePricePerKg = 100m, IsActive = true };
            var color = new Color { Id = Guid.NewGuid(), Name = "Green", Hex = "#00FF00", IsActive = true };
            var category = new Category { Id = Guid.NewGuid(), Name = "Tools", Slug = "tools", IsActive = true, SortOrder = 1 };

            // prices:
            // v1 => (0.100kg * 100) + 20 = 30
            // v2 => (0.200kg * 100) + 1  = 21  (expected min)
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Title = "Clamp",
                Slug = "clamp",
                IsActive = true,
                Categories = new List<Category> { category },
                Variants = new List<ProductVariant>
                {
                    new()
                    {
                        SizeLabel = "Large",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 70,
                        HeightMm = 30,
                        DepthMm = 20,
                        WeightGrams = 100,
                        PriceOffset = 20,
                        IsActive = true
                    },
                    new()
                    {
                        SizeLabel = "Medium",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 60,
                        HeightMm = 25,
                        DepthMm = 20,
                        WeightGrams = 200,
                        PriceOffset = 1,
                        IsActive = true
                    },
                    new()
                    {
                        SizeLabel = "Hidden",
                        MaterialTypeId = material.Id,
                        ColorId = color.Id,
                        WidthMm = 40,
                        HeightMm = 20,
                        DepthMm = 20,
                        WeightGrams = 10,
                        PriceOffset = 0,
                        IsActive = false
                    }
                }
            };

            db.AddRange(material, color, category, product);
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync("/api/v1/catalog/products?category=tools");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<CatalogProductsResponseWithPrice>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(1);

        var item = body.Items.Single();
        item.Slug.Should().Be("clamp");
        item.PriceFrom.Should().Be(21m);
    }

    private static async Task ResetDbAsync(AppDbContext db)
    {
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
        _client.Dispose();
        _api.Dispose();
    }

    private sealed record CatalogProductsResponse(int Total, List<CatalogProductItem> Items);
    private sealed record CatalogProductItem(string Slug);
    private sealed record CatalogProductsResponseWithPrice(int Total, List<CatalogProductItemWithPrice> Items);
    private sealed record CatalogProductItemWithPrice(string Slug, decimal PriceFrom);
    private sealed record CreatedProductResponse(Guid Id);
}
