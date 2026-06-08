namespace ITTicketing.Api.Services;

public sealed class ApiKeyAuthOptions
{
    public const string SectionName = "Auth";

    public string AdminApiKey { get; init; } = string.Empty;
    public string ComplianceApiKey { get; init; } = string.Empty;
}
