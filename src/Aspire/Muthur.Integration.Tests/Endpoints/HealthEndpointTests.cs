using System.Net;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Endpoints;

[Collection("Muthur")]
public sealed class HealthEndpointTests(MuthurFixture platform)
{
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await platform.ApiHttpClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Alive_ReturnsHealthy()
    {
        var response = await platform.ApiHttpClient.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_ReturnsJson()
    {
        var response = await platform.ApiHttpClient.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"openapi\"", content);
        Assert.Contains("/v1/agent/sessions", content);
        Assert.Contains("/v1/documents", content);
    }
}
