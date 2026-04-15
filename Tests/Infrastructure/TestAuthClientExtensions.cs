using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PrintIt.Tests.Infrastructure;

public static class TestAuthClientExtensions
{
    public static async Task LoginAsAdminAsync(this HttpClient client, string email = "admin@test.local", string password = "Admin123!")
    {
        var loginResponse = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new
        {
            email,
            password
        });

        if (loginResponse.StatusCode != HttpStatusCode.OK)
        {
            var body = await loginResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed admin login in test setup. Status={loginResponse.StatusCode}, Body={body}");
        }

        if (!loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            throw new InvalidOperationException("Login response did not include Set-Cookie header.");

        var cookieHeader = setCookieHeaders
            .Select(x => x.Split(';', 2)[0])
            .FirstOrDefault(x => x.StartsWith("printit_admin_auth=", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(cookieHeader))
            throw new InvalidOperationException("Admin auth cookie not found in login response.");

        var rawToken = cookieHeader.Substring("printit_admin_auth=".Length);
        var token = Uri.UnescapeDataString(rawToken).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Admin auth cookie token value is empty.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
    }

    public static void ClearAuthCookie(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove("Cookie");
    }
}