namespace PrintIt.Api.Auth;

public sealed class AuthCookieSettings
{
    public const string SectionName = "AuthCookie";

    public string Name { get; set; } = "printit_admin_auth";

    public string Path { get; set; } = "/";

    public bool HttpOnly { get; set; } = true;

    public bool IsEssential { get; set; } = true;

    public string SameSite { get; set; } = "Lax";

    public bool? Secure { get; set; }
}