using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using PrintIt.Domain.Entities;

namespace PrintIt.Api.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public JwtTokenResult Create(AdminUser adminUser)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_settings.AccessTokenMinutes <= 0 ? 60 : _settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUser.Id.ToString()),
            new(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, adminUser.Email),
            new(ClaimTypes.Email, adminUser.Email),
            new(AdminStoreContext.StoreIdClaimType, adminUser.StoreId.ToString()),
            new(ClaimTypes.Role, "Admin")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new JwtTokenResult(token, expiresAtUtc);
    }
}