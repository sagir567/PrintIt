using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Api.Auth;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Policy = "AdminOnly")]
public class AdminProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminProductsController(AppDbContext db)
    {
        _db = db;
    }

    public record ProductVariantPayload(
        string SizeLabel,
        Guid MaterialTypeId,
        Guid ColorId,
        int WidthMm,
        int HeightMm,
        int DepthMm,
        int WeightGrams,
        decimal PriceOffset,
        bool IsActive);

    public record CreateProductRequest(
        string Title,
        string? Slug,
        string? Description,
        string? MainImageUrl,
        bool IsActive,
        List<Guid>? CategoryIds,
        List<ProductVariantPayload>? Variants);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0) return BadRequest(new { message = "Title is required." });
        if (title.Length > 200) return BadRequest(new { message = "Title must be 200 characters or less." });

        var slug = BuildSlug(request.Slug, title);
        if (slug.Length == 0) return BadRequest(new { message = "Slug is required." });

        var slugExists = await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId && x.Slug == slug);
        if (slugExists) return Conflict(new { message = "Product slug already exists." });

        var categoryIds = request.CategoryIds?.Distinct().ToList() ?? new List<Guid>();
        var categories = categoryIds.Count == 0
            ? new List<Category>()
            : await _db.Categories.IgnoreQueryFilters().Where(x => x.StoreId == storeId && categoryIds.Contains(x.Id)).ToListAsync();

        if (categories.Count != categoryIds.Count)
            return BadRequest(new { message = "One or more categories were not found." });

        var entity = new Product
        {
            StoreId = storeId,
            Title = title,
            Slug = slug,
            Description = NormalizeOptional(request.Description),
            MainImageUrl = NormalizeOptional(request.MainImageUrl),
            IsActive = request.IsActive,
            Categories = categories,
            Variants = new List<ProductVariant>()
        };

        var variants = request.Variants ?? new List<ProductVariantPayload>();
        foreach (var v in variants)
        {
            if (!IsValidVariant(v, out var error)) return BadRequest(new { message = error });

            entity.Variants.Add(new ProductVariant
            {
                SizeLabel = (v.SizeLabel ?? string.Empty).Trim(),
                MaterialTypeId = v.MaterialTypeId,
                ColorId = v.ColorId,
                WidthMm = v.WidthMm,
                HeightMm = v.HeightMm,
                DepthMm = v.DepthMm,
                WeightGrams = v.WeightGrams,
                PriceOffset = v.PriceOffset,
                IsActive = v.IsActive
            });
        }

        _db.Products.Add(entity);
        await _db.SaveChangesAsync();

        return Created($"/api/v1/admin/products/{entity.Id}", new { entity.Id, entity.Title, entity.Slug });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var items = await _db.Products
            .IgnoreQueryFilters()
            .Where(x => x.StoreId == storeId)
            .Include(x => x.Categories)
            .Include(x => x.Variants)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Slug,
                x.Description,
                x.MainImageUrl,
                x.IsActive,
                x.CreatedAtUtc,
                Categories = x.Categories
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => new { c.Id, c.Name, c.Slug, c.IsActive }),
                VariantsCount = x.Variants.Count,
                ActiveVariantsCount = x.Variants.Count(v => v.IsActive)
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateProductRequest request)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var entity = await _db.Products
            .IgnoreQueryFilters()
            .Include(x => x.Categories)
            .Include(x => x.Variants)
            .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);

        if (entity == null) return NotFound();

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0) return BadRequest(new { message = "Title is required." });
        if (title.Length > 200) return BadRequest(new { message = "Title must be 200 characters or less." });

        var slug = BuildSlug(request.Slug, title);
        if (slug.Length == 0) return BadRequest(new { message = "Slug is required." });

        var slugExists = await _db.Products
            .IgnoreQueryFilters()
            .AnyAsync(x => x.StoreId == storeId && x.Id != id && x.Slug == slug);
        if (slugExists) return Conflict(new { message = "Product slug already exists." });

        var categoryIds = request.CategoryIds?.Distinct().ToList() ?? new List<Guid>();
        var categories = categoryIds.Count == 0
            ? new List<Category>()
            : await _db.Categories.IgnoreQueryFilters().Where(x => x.StoreId == storeId && categoryIds.Contains(x.Id)).ToListAsync();

        if (categories.Count != categoryIds.Count)
            return BadRequest(new { message = "One or more categories were not found." });

        var variants = request.Variants ?? new List<ProductVariantPayload>();
        var existingVariants = entity.Variants.ToList();
        var existingByKey = existingVariants.ToDictionary(v => BuildVariantKey(v.SizeLabel, v.MaterialTypeId, v.ColorId));
        var requestedVariantKeys = new HashSet<string>();

        foreach (var v in variants)
        {
            if (!IsValidVariant(v, out var error)) return BadRequest(new { message = error });

            var sizeLabel = (v.SizeLabel ?? string.Empty).Trim();
            var key = BuildVariantKey(sizeLabel, v.MaterialTypeId, v.ColorId);

            if (!requestedVariantKeys.Add(key))
                return BadRequest(new { message = "Duplicate variants are not allowed." });

            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.SizeLabel = sizeLabel;
                existing.MaterialTypeId = v.MaterialTypeId;
                existing.ColorId = v.ColorId;
                existing.WidthMm = v.WidthMm;
                existing.HeightMm = v.HeightMm;
                existing.DepthMm = v.DepthMm;
                existing.WeightGrams = v.WeightGrams;
                existing.PriceOffset = v.PriceOffset;
                existing.IsActive = v.IsActive;
                continue;
            }

            _db.ProductVariants.Add(new ProductVariant
            {
                ProductId = entity.Id,
                SizeLabel = sizeLabel,
                MaterialTypeId = v.MaterialTypeId,
                ColorId = v.ColorId,
                WidthMm = v.WidthMm,
                HeightMm = v.HeightMm,
                DepthMm = v.DepthMm,
                WeightGrams = v.WeightGrams,
                PriceOffset = v.PriceOffset,
                IsActive = v.IsActive
            });
        }

        entity.Title = title;
        entity.Slug = slug;
        entity.Description = NormalizeOptional(request.Description);
        entity.MainImageUrl = NormalizeOptional(request.MainImageUrl);
        entity.IsActive = request.IsActive;

        entity.Categories.Clear();
        foreach (var category in categories) entity.Categories.Add(category);

        var variantsToRemove = existingVariants
            .Where(v => !requestedVariantKeys.Contains(BuildVariantKey(v.SizeLabel, v.MaterialTypeId, v.ColorId)))
            .ToList();

        if (variantsToRemove.Count != 0)
            _db.ProductVariants.RemoveRange(variantsToRemove);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var entity = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
        if (entity == null) return NotFound();
        if (!entity.IsActive) return NoContent();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var entity = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
        if (entity == null) return NotFound();
        if (entity.IsActive) return NoContent();

        entity.IsActive = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> HardDelete(Guid id)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var entity = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
        if (entity == null) return NotFound();

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static bool IsValidVariant(ProductVariantPayload variant, out string error)
    {
        error = string.Empty;
        var sizeLabel = (variant.SizeLabel ?? string.Empty).Trim();
        if (sizeLabel.Length == 0)
        {
            error = "Variant size label is required.";
            return false;
        }

        if (sizeLabel.Length > 50)
        {
            error = "Variant size label must be 50 characters or less.";
            return false;
        }

        if (variant.WidthMm <= 0 || variant.HeightMm <= 0 || variant.DepthMm <= 0)
        {
            error = "Variant dimensions must be positive numbers.";
            return false;
        }

        if (variant.WeightGrams <= 0)
        {
            error = "Variant weight must be greater than 0.";
            return false;
        }

        if (variant.PriceOffset < 0)
        {
            error = "Variant price offset must be 0 or greater.";
            return false;
        }

        return true;
    }

    private static string NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string BuildSlug(string? explicitSlug, string fallbackTitle)
    {
        var source = string.IsNullOrWhiteSpace(explicitSlug) ? fallbackTitle : explicitSlug;
        var cleaned = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        return cleaned.Trim('-');
    }

    private static string BuildVariantKey(string sizeLabel, Guid materialTypeId, Guid colorId)
        => $"{sizeLabel.Trim()}::{materialTypeId:N}::{colorId:N}";
}
