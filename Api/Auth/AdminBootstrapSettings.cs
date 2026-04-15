namespace PrintIt.Api.Auth;

public sealed class AdminBootstrapSettings
{
    public const string SectionName = "AdminBootstrap";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}