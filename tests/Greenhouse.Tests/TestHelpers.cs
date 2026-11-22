using System.Net.Http.Json;
using Greenhouse.Core.Contracts;

namespace Greenhouse.Tests;

internal static class TestHelpers
{
    public static async Task<T> GetAsync<T>(this HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url, GreenhouseFactory.Json))!;
}
