using System.Net;
using AwesomeAssertions;

namespace TransBrain.Api.IntegrationTests;

public class AuthorizationFallbackTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    [Fact]
    public async Task GetHealth_WithoutToken_ReturnsSuccess()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAlive_WithoutToken_ReturnsSuccess()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ASP.NET Core's fallback authorization policy applies to any request that has no endpoint
    // metadata to inspect, and an unmatched route (no endpoint at all) falls into that same
    // bucket - not just endpoints that forgot RequireAuthorization. There is no endpoint here to
    // exempt with AllowAnonymous(), so this is accepted deliberately rather than fixed: a uniform
    // 401 for every unauthenticated request means an anonymous caller cannot distinguish "this
    // route doesn't exist" from "this route exists but you're not authenticated", which prevents
    // enumerating the API surface by watching status codes. The accepted trade-off is that a
    // developer who mistypes a URL sees 401 instead of 404.
    [Fact]
    public async Task GetUnmappedRoute_WithoutToken_ReturnsUnauthorizedBecauseTheFallbackPolicyCoversUnmatchedRoutes()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Pins the distinction the test above relies on: the fallback policy is about authentication,
    // not about hiding routes from legitimate callers. Once a caller is authenticated, an
    // unmatched route falls through to the normal "no endpoint matched" 404.
    [Fact]
    public async Task GetUnmappedRoute_WithToken_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
