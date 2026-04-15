using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrintIt.Api.Auth;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOptions<AuthCookieSettings> _cookieSettings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminAuthController> _logger;

    public AdminAuthController(
        AppDbContext db,
        IPasswordHasher<AdminUser> passwordHasher,
        IJwtTokenService jwtTokenService,
        IOptions<AuthCookieSettings> cookieSettings,
        IWebHostEnvironment environment,
        ILogger<AdminAuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _cookieSettings = cookieSettings;
        _environment = environment;
        _logger = logger;
    }

    public record LoginRequest(string Email, string Password);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Unauthorized(new { message = "Invalid credentials." });

        var normalizedEmail = NormalizeEmail(email);
        var adminUser = await _db.AdminUsers
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

        if (adminUser == null || !adminUser.IsActive)
            return Unauthorized(new { message = "Invalid credentials." });

        var verification = _passwordHasher.VerifyHashedPassword(adminUser, adminUser.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid credentials." });

        adminUser.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwtTokenService.Create(adminUser);
        Response.Cookies.Append(GetCookieName(), token.AccessToken, BuildCookieOptions(token.ExpiresAtUtc));

        return Ok(new
        {
            expiresAtUtc = token.ExpiresAtUtc,
            admin = new
            {
                adminUser.Id,
                adminUser.Email
            }
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(GetCookieName(), BuildCookieOptions());
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var idRaw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var storeIdRaw = User.FindFirstValue(AdminStoreContext.StoreIdClaimType);

        if (!Guid.TryParse(idRaw, out var id))
            return Unauthorized();

        if (!Guid.TryParse(storeIdRaw, out var storeId))
            return Unauthorized();

        return Ok(new
        {
            id,
            email,
            storeId
        });
    }

    private string GetCookieName()
        => string.IsNullOrWhiteSpace(_cookieSettings.Value.Name)
            ? "printit_admin_auth"
            : _cookieSettings.Value.Name;

    private CookieOptions BuildCookieOptions(DateTime? expiresAtUtc = null)
    {
        var settings = _cookieSettings.Value;
        var sameSite = ParseSameSite(settings.SameSite);
        var secure = settings.Secure ?? !_environment.IsDevelopment();

        if (sameSite == SameSiteMode.None && !secure)
        {
            _logger.LogWarning("Auth cookie configured with SameSite=None and Secure=false. This may be blocked by browsers outside local development.");
        }

        var options = new CookieOptions
        {
            HttpOnly = settings.HttpOnly,
            IsEssential = settings.IsEssential,
            Path = string.IsNullOrWhiteSpace(settings.Path) ? "/" : settings.Path,
            SameSite = sameSite,
            Secure = secure
        };

        if (expiresAtUtc.HasValue)
            options.Expires = expiresAtUtc.Value;

        return options;
    }

    private static SameSiteMode ParseSameSite(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            "unspecified" => SameSiteMode.Unspecified,
            _ => SameSiteMode.Lax
        };

    private static string NormalizeEmail(string value)
        => value.Trim().ToUpperInvariant();
}