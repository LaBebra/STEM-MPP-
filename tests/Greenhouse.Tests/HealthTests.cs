using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Greenhouse.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
