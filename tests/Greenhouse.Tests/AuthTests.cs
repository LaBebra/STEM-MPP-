using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Greenhouse.Core.Contracts;
using Greenhouse.Core.Domain;

namespace Greenhouse.Tests;

public class AuthTests : IDisposable
{
    private readonly GreenhouseFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Writing_without_api_key_is_401()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostReadingAsync(new { sensor = "temp-1", value = 20 })).StatusCode);
    }

    [Fact]
    public async Task Writing_with_wrong_api_key_is_401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostBatchAsync(new[] { new { sensor = "temp-1", value = 20 } })).StatusCode);
    }

    [Fact]
    public async Task Operator_token_cannot_write_readings()
    {
        using var op = await _factory.CreateUserClientAsync(Roles.Operator);
        var response = await op.PostReadingAsync(new { sensor = "temp-1", value = 20 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sensor_key_cannot_acknowledge_alerts()
    {
        using var sensor = _factory.CreateSensorClient();
        var response = await sensor.PostAsync(new Uri("/api/alerts/1/ack", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forged_token_is_rejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.e30.abc");
        var response = await client.PostAsync(new Uri("/api/alerts/1/ack", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("nobody", "admin-password")]
    [InlineData(null, null)]
    public async Task Wrong_credentials_get_401(string? username, string? password)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), GreenhouseFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reading_data_needs_no_auth()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/readings/latest", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/alerts", UriKind.Relative))).StatusCode);
    }
}
