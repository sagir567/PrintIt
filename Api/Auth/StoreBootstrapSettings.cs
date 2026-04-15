namespace PrintIt.Api.Auth;

public sealed class StoreBootstrapSettings
{
    public const string SectionName = "StoreBootstrap";

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}