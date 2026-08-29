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

    [Fact]
    public async Task GetUnmappedRoute_WithoutToken_ReturnsNotFoundNotUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
