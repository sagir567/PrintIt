using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/catalog")]
public class CatalogController : ControllerBase
{
    private readonly AppDbContext _db;

    public CatalogController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Categories
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                x.Description,
                x.SortOrder
            })
            .ToListAsync();

        return Ok(categories);
    }

[HttpGet("products")]
public async Task<IActionResult> GetProducts(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] string sort = "newest",
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

    var products = _db.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            products = products.Where(x =>
                EF.Functions.ILike(x.Title, $"%{term}%") ||
                (x.Description != null && EF.Functions.ILike(x.Description, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var catSlug = category.Trim().ToLowerInvariant();
            products = products.Where(x => x.Categories.Any(c => c.IsActive && c.Slug == catSlug));
        }

        var projected = products.Select(x => new ProductCardDto
        {
            Id = x.Id,
            Title = x.Title,
            Slug = x.Slug,
            Description = x.Description ?? string.Empty,
            MainImageUrl = x.MainImageUrl,
            CreatedAtUtc = x.CreatedAtUtc,
            PriceFrom = x.Variants
                .Where(v => v.IsActive)
                .Min(v => (decimal?)(((decimal)v.WeightGrams / 1000m) * v.MaterialType.BasePricePerKg + v.PriceOffset)) ?? 0m,
            ActiveVariantsCount = x.Variants.Count(v => v.IsActive)
        });

        if (minPrice.HasValue)
            projected = projected.Where(x => x.PriceFrom >= minPrice.Value);
        if (maxPrice.HasValue)
            projected = projected.Where(x => x.PriceFrom <= maxPrice.Value);

        projected = sort?.ToLowerInvariant() switch
        {
            "name_asc" => projected.OrderBy(x => x.Title),
            "name_desc" => projected.OrderByDescending(x => x.Title),
            "price_asc" => projected.OrderBy(x => x.PriceFrom).ThenByDescending(x => x.CreatedAtUtc),
            "price_desc" => projected.OrderByDescending(x => x.PriceFrom).ThenByDescending(x => x.CreatedAtUtc),
            _ => projected.OrderByDescending(x => x.CreatedAtUtc)
        };

        var total = await projected.CountAsync();
        var pageItems = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var productIds = pageItems.Select(x => x.Id).ToList();
        var categoryRows = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .SelectMany(p => p.Categories
                .Where(c => c.IsActive)
                .Select(c => new
                {
                    ProductId = p.Id,
                    CategoryId = c.Id,
                    c.Name,
                    c.Slug,
                    c.SortOrder
                }))
            .ToListAsync();

        var categoriesByProduct = categoryRows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Name)
                    .Select(x => new CategoryDto
                    {
                        Id = x.CategoryId,
                        Name = x.Name,
                        Slug = x.Slug
                    })
                    .ToList());

    var items = pageItems.Select(x => new ProductCardDto
    {
        Id = x.Id,
        Title = x.Title,
        Slug = x.Slug,
        Description = x.Description,
        MainImageUrl = x.MainImageUrl,
        CreatedAtUtc = x.CreatedAtUtc,
        PriceFrom = x.PriceFrom,
        ActiveVariantsCount = x.ActiveVariantsCount,
        Categories = categoriesByProduct.TryGetValue(x.Id, out var list) ? list : new List<CategoryDto>()
    }).ToList();

        return Ok(new
        {
            total,
            page,
            pageSize,
            hasNext = page * pageSize < total,
            items
        });
    }

    [HttpGet("products/{slug}")]
    public async Task<IActionResult> GetProductBySlug(string slug)
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();

        var product = await _db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.Slug == normalized)
            .Include(x => x.Categories)
            .Include(x => x.Variants)
                .ThenInclude(v => v.MaterialType)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Color)
            .Select(x => new ProductDetailsDto
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Description = x.Description ?? string.Empty,
                MainImageUrl = x.MainImageUrl,
                CreatedAtUtc = x.CreatedAtUtc,
                Categories = x.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => new CategoryDto { Id = c.Id, Name = c.Name, Slug = c.Slug })
                    .ToList(),
                Variants = x.Variants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.SizeLabel)
                    .Select(v => new ProductVariantDto
                    {
                        Id = v.Id,
                        SizeLabel = v.SizeLabel,
                        WidthMm = v.WidthMm,
                        HeightMm = v.HeightMm,
                        DepthMm = v.DepthMm,
                        WeightGrams = v.WeightGrams,
                        PriceOffset = v.PriceOffset,
                        MaterialType = new MaterialDto
                        {
                            MaterialTypeId = v.MaterialTypeId,
                            Name = v.MaterialType.Name,
                            BasePricePerKg = v.MaterialType.BasePricePerKg
                        },
                        Color = new ColorDto
                        {
                            ColorId = v.ColorId,
                            Name = v.Color.Name,
                            Hex = v.Color.Hex
                        },
                        Price = ((decimal)v.WeightGrams / 1000m) * v.MaterialType.BasePricePerKg + v.PriceOffset
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (product == null) return NotFound();
        return Ok(product);
    }

    private sealed class ProductCardDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? MainImageUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public decimal PriceFrom { get; set; }
        public int ActiveVariantsCount { get; set; }
        public List<CategoryDto> Categories { get; set; } = new();
    }

    private sealed class ProductDetailsDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? MainImageUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ProductVariantDto> Variants { get; set; } = new();
    }

    private sealed class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }

    private sealed class MaterialDto
    {
        public Guid MaterialTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal BasePricePerKg { get; set; }
    }

    private sealed class ColorDto
    {
        public Guid ColorId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;
    }

    private sealed class ProductVariantDto
    {
        public Guid Id { get; set; }
        public string SizeLabel { get; set; } = string.Empty;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int DepthMm { get; set; }
        public int WeightGrams { get; set; }
        public decimal PriceOffset { get; set; }
        public MaterialDto MaterialType { get; set; } = new();
        public ColorDto Color { get; set; } = new();
        public decimal Price { get; set; }
    }
}
