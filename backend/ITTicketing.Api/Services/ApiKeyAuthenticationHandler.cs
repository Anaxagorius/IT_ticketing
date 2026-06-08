using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ITTicketing.Api.Services;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyAuthOptions> apiKeyOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string AdminRole = "Admin";
    public const string ComplianceRole = "Compliance";
    private const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = headerValues.ToString();

        var authOptions = apiKeyOptions.Value;
        var roles = new List<string>();

        if (!string.IsNullOrWhiteSpace(authOptions.AdminApiKey) &&
            string.Equals(providedKey, authOptions.AdminApiKey, StringComparison.Ordinal))
        {
            roles.Add(AdminRole);
        }

        if (!string.IsNullOrWhiteSpace(authOptions.ComplianceApiKey) &&
            string.Equals(providedKey, authOptions.ComplianceApiKey, StringComparison.Ordinal))
        {
            roles.Add(ComplianceRole);
        }

        if (roles.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = roles
            .Select(role => new Claim(ClaimTypes.Role, role))
            .Append(new Claim(ClaimTypes.Name, "api-key-user"))
            .ToList();

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
