using System.Net;
using System.Net.Http.Json;
using Muthur.Contracts;
using Muthur.Integration.Tests.Infrastructure;

namespace Muthur.Integration.Tests.Endpoints;

[Collection("Muthur")]
public sealed class DocumentEndpointTests(MuthurFixture platform)
{
    [Fact]
    public async Task ListDocuments_ReturnsOk()
    {
        var response = await platform.ApiHttpClient.GetAsync("/v1/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var docs = await response.Content.ReadFromJsonAsync<List<DocumentSummary>>();
        Assert.NotNull(docs);
    }

    [Fact]
    public async Task GetDocument_UnknownId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var response = await platform.ApiHttpClient.GetAsync($"/v1/documents/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDocumentContent_UnknownId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var response = await platform.ApiHttpClient.GetAsync($"/v1/documents/{id}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
