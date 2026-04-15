using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Api.Auth;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Policy = "AdminOnly")]
public class AdminCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateCategoryRequest(string Name, string? Slug, string? Description, int SortOrder);
    public record UpdateCategoryRequest(string Name, string? Slug, string? Description, int SortOrder, bool IsActive);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0) return BadRequest(new { message = "Name is required." });
        if (name.Length > 100) return BadRequest(new { message = "Name must be 100 characters or less." });

        var slug = BuildSlug(request.Slug, name);
        if (slug.Length == 0) return BadRequest(new { message = "Slug is required." });

        var exists = await _db.Categories.IgnoreQueryFilters().AnyAsync(x => x.StoreId == storeId && (x.Name == name || x.Slug == slug));
        if (exists) return Conflict(new { message = "Category with same name or slug already exists." });

        var entity = new Category
        {
            StoreId = storeId,
            Name = name,
            Slug = slug,
            Description = NormalizeOptional(request.Description),
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync();

        return Created($"/api/v1/admin/categories/{entity.Id}", new
        {
            entity.Id,
            entity.Name,
            entity.Slug,
            entity.Description,
            entity.SortOrder,
            entity.IsActive
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var items = await _db.Categories
            .IgnoreQueryFilters()
            .Where(x => x.StoreId == storeId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                x.Description,
                x.SortOrder,
                x.IsActive,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var entity = await _db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
        if (entity == null) return NotFound();

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0) return BadRequest(new { message = "Name is required." });
        if (name.Length > 100) return BadRequest(new { message = "Name must be 100 characters or less." });

        var slug = BuildSlug(request.Slug, name);
        if (slug.Length == 0) return BadRequest(new { message = "Slug is required." });

        var duplicate = await _db.Categories
            .IgnoreQueryFilters()
            .AnyAsync(x => x.StoreId == storeId && x.Id != id && (x.Name == name || x.Slug == slug));

        if (duplicate) return Conflict(new { message = "Category with same name or slug already exists." });

        entity.Name = name;
        entity.Slug = slug;
        entity.Description = NormalizeOptional(request.Description);
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
            return Forbid();

        var entity = await _db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
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

        var entity = await _db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
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

        var entity = await _db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == id);
        if (entity == null) return NotFound();

        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string BuildSlug(string? explicitSlug, string fallbackName)
    {
        var source = string.IsNullOrWhiteSpace(explicitSlug) ? fallbackName : explicitSlug;
        var cleaned = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        return cleaned.Trim('-');
    }
}
