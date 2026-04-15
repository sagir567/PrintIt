using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Tests.Infrastructure;

namespace PrintIt.Tests.Controllers;

public sealed class AdminProductsControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public AdminProductsControllerTests(PostgresFixture pg)
    {
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
        _client.LoginAsAdminAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Admin_products_should_return_unauthorized_without_auth_cookie()
    {
        _client.ClearAuthCookie();

        var resp = await _client.GetAsync("/api/v1/admin/products");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _client.LoginAsAdminAsync();
    }

    [Fact]
    public async Task Admin_products_should_support_search_and_phase1_sort_modes()
    {
        Guid plaId;
        Guid blackId;

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            plaId = Guid.NewGuid();
            blackId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType
            {
                Id = plaId,
                StoreId = StoreConstants.BootstrapStoreId,
                Name = "PLA",
                BasePricePerKg = 120m,
                IsActive = true
            });

            db.Colors.Add(new Color
            {
                Id = blackId,
                StoreId = StoreConstants.BootstrapStoreId,
                Name = "Black",
                Hex = "#000000",
                IsActive = true
            });

            await db.SaveChangesAsync();
        }

        var alphaId = await CreateProductAsync("Alpha Holder", "alpha-holder", true, plaId, blackId);
        var betaId = await CreateProductAsync("Beta Stand", "beta-stand", false, plaId, blackId);
        var gammaId = await CreateProductAsync("Gamma Rack", "gamma-rack", true, plaId, blackId);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var alpha = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == alphaId);
            var beta = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == betaId);
            var gamma = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == gammaId);

            alpha.CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            beta.CreatedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            gamma.CreatedAtUtc = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

            await db.SaveChangesAsync();
        }

        var newestItems = await _client.GetFromJsonAsync<List<AdminProductListItemResponse>>("/api/v1/admin/products?sort=newest");
        newestItems.Should().NotBeNull();
        newestItems!.Select(x => x.Title).Should().ContainInOrder("Gamma Rack", "Beta Stand", "Alpha Holder");

        var alphabeticalItems = await _client.GetFromJsonAsync<List<AdminProductListItemResponse>>("/api/v1/admin/products?sort=alphabetical");
        alphabeticalItems.Should().NotBeNull();
        alphabeticalItems!.Select(x => x.Title).Should().ContainInOrder("Alpha Holder", "Beta Stand", "Gamma Rack");

        var activeFirstItems = await _client.GetFromJsonAsync<List<AdminProductListItemResponse>>("/api/v1/admin/products?sort=active_first");
        activeFirstItems.Should().NotBeNull();
        activeFirstItems!.First().IsActive.Should().BeTrue();
        activeFirstItems.Last().IsActive.Should().BeFalse();

        var inactiveFirstItems = await _client.GetFromJsonAsync<List<AdminProductListItemResponse>>("/api/v1/admin/products?sort=inactive_first");
        inactiveFirstItems.Should().NotBeNull();
        inactiveFirstItems!.First().IsActive.Should().BeFalse();

        var searchItems = await _client.GetFromJsonAsync<List<AdminProductListItemResponse>>("/api/v1/admin/products?q=beta");
        searchItems.Should().NotBeNull();
        searchItems!.Should().HaveCount(1);
        searchItems[0].Title.Should().Be("Beta Stand");
    }

    [Fact]
    public async Task Admin_product_details_endpoint_should_return_single_product_for_current_store()
    {
        Guid plaId;
        Guid blackId;
        Guid categoryId;

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            plaId = Guid.NewGuid();
            blackId = Guid.NewGuid();
            categoryId = Guid.NewGuid();

            db.MaterialTypes.Add(new MaterialType
            {
                Id = plaId,
                StoreId = StoreConstants.BootstrapStoreId,
                Name = "PLA",
                BasePricePerKg = 110m,
                IsActive = true
            });

            db.Colors.Add(new Color
            {
                Id = blackId,
                StoreId = StoreConstants.BootstrapStoreId,
                Name = "Black",
                Hex = "#000000",
                IsActive = true
            });

            db.Categories.Add(new Category
            {
                Id = categoryId,
                StoreId = StoreConstants.BootstrapStoreId,
                Name = "Desk",
                Slug = "desk",
                IsActive = true,
                SortOrder = 1
            });

            await db.SaveChangesAsync();
        }

        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/products", new
        {
            title = "Desk Tray",
            slug = "desk-tray",
            description = "Useful tray",
            mainImageUrl = "https://img.example.com/tray.png",
            isActive = true,
            categoryIds = new[] { categoryId },
            variants = new[]
            {
                new
                {
                    sizeLabel = "Standard",
                    materialTypeId = plaId,
                    colorId = blackId,
                    widthMm = 120,
                    heightMm = 18,
                    depthMm = 80,
                    weightGrams = 95,
                    priceOffset = 7,
                    isActive = true
                }
            }
        });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedProductResponse>();
        created.Should().NotBeNull();

        var detailsResp = await _client.GetAsync($"/api/v1/admin/products/{created!.Id}");
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var details = await detailsResp.Content.ReadFromJsonAsync<AdminProductDetailsResponse>();
        details.Should().NotBeNull();
        details!.Title.Should().Be("Desk Tray");
        details.Slug.Should().Be("desk-tray");
        details.IsActive.Should().BeTrue();
        details.Categories.Should().ContainSingle(c => c.Slug == "desk");
        details.Variants.Should().ContainSingle(v =>
            v.SizeLabel == "Standard" &&
            v.MaterialTypeId == plaId &&
            v.ColorId == blackId);
        details.ActiveVariantsCount.Should().Be(1);
        details.VariantsCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_should_update_metadata_replace_variants_keep_same_id_and_reflect_in_catalog_and_details()
    {
        Guid plaId;
        Guid petgId;
        Guid blackId;
        Guid whiteId;
        Guid categoryId;

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ResetDbAsync(db);

            plaId = Guid.NewGuid();
            petgId = Guid.NewGuid();
            blackId = Guid.NewGuid();
            whiteId = Guid.NewGuid();
            categoryId = Guid.NewGuid();

            db.MaterialTypes.AddRange(
                new MaterialType { Id = plaId, Name = "PLA", BasePricePerKg = 140m, IsActive = true },
                new MaterialType { Id = petgId, Name = "PETG", BasePricePerKg = 170m, IsActive = true }
            );

            db.Colors.AddRange(
                new Color { Id = blackId, Name = "Black", Hex = "#000000", IsActive = true },
                new Color { Id = whiteId, Name = "White", Hex = "#FFFFFF", IsActive = true }
            );

            db.Categories.Add(new Category
            {
                Id = categoryId,
                Name = "Organizers",
                Slug = "organizers",
                IsActive = true,
                SortOrder = 1
            });

            await db.SaveChangesAsync();
        }

        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/products", new
        {
            title = "Cable Organizer",
            slug = "cable-organizer",
            description = "Old description",
            mainImageUrl = "https://img.example.com/old.png",
            isActive = true,
            categoryIds = new[] { categoryId },
            variants = new[]
            {
                new
                {
                    sizeLabel = "S",
                    materialTypeId = plaId,
                    colorId = blackId,
                    widthMm = 50,
                    heightMm = 20,
                    depthMm = 15,
                    weightGrams = 80,
                    priceOffset = 3,
                    isActive = true
                }
            }
        });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedProductResponse>();
        created.Should().NotBeNull();

        var productId = created!.Id;

        var updateResp = await _client.PutAsJsonAsync($"/api/v1/admin/products/{productId}", new
        {
            title = "Cable Organizer Pro",
            slug = "cable-organizer-pro",
            description = "Updated description",
            mainImageUrl = "https://img.example.com/new.png",
            isActive = true,
            categoryIds = new[] { categoryId },
            variants = new[]
            {
                new
                {
                    sizeLabel = "M",
                    materialTypeId = plaId,
                    colorId = whiteId,
                    widthMm = 70,
                    heightMm = 30,
                    depthMm = 20,
                    weightGrams = 120,
                    priceOffset = 6,
                    isActive = true
                },
                new
                {
                    sizeLabel = "L",
                    materialTypeId = petgId,
                    colorId = blackId,
                    widthMm = 90,
                    heightMm = 35,
                    depthMm = 25,
                    weightGrams = 180,
                    priceOffset = 9,
                    isActive = true
                }
            }
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var verifyScope = _api.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var updated = await db.Products
                .IgnoreQueryFilters()
                .Include(x => x.Variants)
                .SingleAsync(x => x.Id == productId);

            updated.Id.Should().Be(productId);
            updated.Title.Should().Be("Cable Organizer Pro");
            updated.Slug.Should().Be("cable-organizer-pro");
            updated.Description.Should().Be("Updated description");
            updated.MainImageUrl.Should().Be("https://img.example.com/new.png");

            updated.Variants.Should().HaveCount(2);
            updated.Variants.Should().NotContain(v =>
                v.SizeLabel == "S" && v.MaterialTypeId == plaId && v.ColorId == blackId);
            updated.Variants.Should().Contain(v =>
                v.SizeLabel == "M" && v.MaterialTypeId == plaId && v.ColorId == whiteId);
            updated.Variants.Should().Contain(v =>
                v.SizeLabel == "L" && v.MaterialTypeId == petgId && v.ColorId == blackId);
        }

        var catalogResp = await _client.GetAsync("/api/v1/catalog/products?category=organizers");
        catalogResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalog = await catalogResp.Content.ReadFromJsonAsync<CatalogProductsResponse>();
        catalog.Should().NotBeNull();
        catalog!.Total.Should().Be(1);

        var catalogItem = catalog.Items.Single();
        catalogItem.Id.Should().Be(productId);
        catalogItem.Title.Should().Be("Cable Organizer Pro");
        catalogItem.Slug.Should().Be("cable-organizer-pro");
        catalogItem.Description.Should().Be("Updated description");
        catalogItem.MainImageUrl.Should().Be("https://img.example.com/new.png");
        catalogItem.ActiveVariantsCount.Should().Be(2);

        var detailsResp = await _client.GetAsync("/api/v1/catalog/products/cable-organizer-pro");
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var details = await detailsResp.Content.ReadFromJsonAsync<ProductDetailsResponse>();
        details.Should().NotBeNull();
        details!.Id.Should().Be(productId);
        details.Title.Should().Be("Cable Organizer Pro");
        details.Slug.Should().Be("cable-organizer-pro");
        details.Description.Should().Be("Updated description");
        details.MainImageUrl.Should().Be("https://img.example.com/new.png");
        details.Variants.Should().HaveCount(2);
        details.Variants.Should().Contain(v =>
            v.SizeLabel == "M" && v.MaterialType.MaterialTypeId == plaId && v.Color.ColorId == whiteId);
        details.Variants.Should().Contain(v =>
            v.SizeLabel == "L" && v.MaterialType.MaterialTypeId == petgId && v.Color.ColorId == blackId);
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

    private async Task<Guid> CreateProductAsync(
        string title,
        string slug,
        bool isActive,
        Guid materialTypeId,
        Guid colorId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/admin/products", new
        {
            title,
            slug,
            description = string.Empty,
            mainImageUrl = string.Empty,
            isActive,
            variants = new[]
            {
                new
                {
                    sizeLabel = "Default",
                    materialTypeId,
                    colorId,
                    widthMm = 100,
                    heightMm = 40,
                    depthMm = 60,
                    weightGrams = 120,
                    priceOffset = 5,
                    isActive = true
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatedProductResponse>();
        created.Should().NotBeNull();
        return created!.Id;
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    private sealed record CreatedProductResponse(Guid Id);
    private sealed record CatalogProductsResponse(int Total, List<CatalogProductItem> Items);
    private sealed record CatalogProductItem(
        Guid Id,
        string Title,
        string Slug,
        string Description,
        string? MainImageUrl,
        int ActiveVariantsCount);

    private sealed record ProductDetailsResponse(
        Guid Id,
        string Title,
        string Slug,
        string Description,
        string? MainImageUrl,
        List<ProductVariantDetailsItem> Variants);

    private sealed record ProductVariantDetailsItem(
        string SizeLabel,
        MaterialTypeSummary MaterialType,
        ColorSummary Color);

    private sealed record AdminProductListItemResponse(
        Guid Id,
        string Title,
        string Slug,
        bool IsActive,
        DateTime CreatedAtUtc,
        int VariantsCount,
        int ActiveVariantsCount);

    private sealed record AdminProductDetailsResponse(
        Guid Id,
        string Title,
        string Slug,
        string Description,
        string? MainImageUrl,
        bool IsActive,
        int VariantsCount,
        int ActiveVariantsCount,
        List<AdminCategorySummary> Categories,
        List<AdminVariantSummary> Variants);

    private sealed record AdminCategorySummary(Guid Id, string Name, string Slug, bool IsActive);

    private sealed record AdminVariantSummary(
        Guid Id,
        string SizeLabel,
        Guid MaterialTypeId,
        string MaterialTypeName,
        Guid ColorId,
        string ColorName,
        string? ColorHex,
        int WidthMm,
        int HeightMm,
        int DepthMm,
        int WeightGrams,
        decimal PriceOffset,
        bool IsActive);

    private sealed record MaterialTypeSummary(Guid MaterialTypeId, string Name, decimal BasePricePerKg);
    private sealed record ColorSummary(Guid ColorId, string Name, string Hex);
}