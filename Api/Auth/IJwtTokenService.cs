using PrintIt.Domain.Entities;

namespace PrintIt.Api.Auth;

public interface IJwtTokenService
{
    JwtTokenResult Create(AdminUser adminUser);
}

public sealed record JwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);