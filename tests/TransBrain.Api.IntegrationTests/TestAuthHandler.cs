using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TransBrain.Api.IntegrationTests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";
    public const string RolesHeader = "X-Test-Roles";
    public const string SubjectHeader = "X-Test-Subject";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues roles))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Defaults to the old constant so every existing test keeps its current identity; only
        // a test that sets the header gets a different subject.
        string subject = Request.Headers.TryGetValue(
                             SubjectHeader, out Microsoft.Extensions.Primitives.StringValues header)
                         && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : "test-user";

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject),
            // The Api's HttpContextCurrentUser reads "sub" first and only falls back to
            // NameIdentifier, so the driver-scoping path is exercised through the same claim
            // Keycloak actually issues rather than through the fallback.
            new("sub", subject),
            .. roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role))
        ];

        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
