using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PrintIt.Tests.Infrastructure;

namespace PrintIt.Tests.Controllers;

public sealed class AdminAuthControllerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public AdminAuthControllerTests(PostgresFixture pg)
    {
        _api = new ApiFactory(pg.ConnectionString);
        _client = _api.CreateClient();
    }

    [Fact]
    public async Task Login_should_succeed_with_valid_credentials_and_set_auth_cookie()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/admin/auth/login", new
        {
            email = "admin@test.local",
            password = "Admin123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().NotBeNull();
        cookies!.Any(x => x.StartsWith("printit_admin_auth=", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task Login_should_fail_with_invalid_credentials()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/admin/auth/login", new
        {
            email = "admin@test.local",
            password = "wrong-password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_should_return_unauthorized_without_auth_cookie()
    {
        var response = await _client.GetAsync("/api/v1/admin/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_should_return_admin_identity_with_valid_auth_cookie()
    {
        await _client.LoginAsAdminAsync();

        var response = await _client.GetAsync("/api/v1/admin/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminMeResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("admin@test.local");
        body.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Logout_should_clear_cookie_and_block_access_afterwards()
    {
        await _client.LoginAsAdminAsync();

        var logoutResponse = await _client.PostAsync("/api/v1/admin/auth/logout", content: null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logoutResponse.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().NotBeNull();
        cookies!.Any(x => x.StartsWith("printit_admin_auth=", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        _client.ClearAuthCookie();
        var meResponse = await _client.GetAsync("/api/v1/admin/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    private sealed record AdminMeResponse(Guid Id, string Email);
}