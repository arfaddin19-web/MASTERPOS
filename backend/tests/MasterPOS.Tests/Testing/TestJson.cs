using System.Net.Http.Json;
using System.Text.Json;

namespace MasterPOS.Tests.Testing;

/// <summary>
/// The API returns camelCase JSON (ASP.NET Core's default), but the DTO
/// records reused here from MasterPOS.Application are PascalCase — and
/// <see cref="System.Text.Json.JsonSerializer"/>'s own default options are
/// case-sensitive, unlike the case-insensitive binder ASP.NET Core wires up
/// for controller *input*. Every test HTTP call goes through these helpers
/// so that mismatch never silently deserializes into a half-null object.
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(Options);
        return value ?? throw new InvalidOperationException($"Response body deserialized to null for {typeof(T).Name}.");
    }

    public static Task<HttpResponseMessage> PostJsonAsync<TBody>(this HttpClient client, string url, TBody body)
        => client.PostAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> PutJsonAsync<TBody>(this HttpClient client, string url, TBody body)
        => client.PutAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> PatchJsonAsync<TBody>(this HttpClient client, string url, TBody body)
        => client.PatchAsync(url, JsonContent.Create(body, options: Options));

    /// <summary>Wraps <see cref="HttpClientJsonExtensions.GetFromJsonAsync{TValue}(HttpClient, string, JsonSerializerOptions?, CancellationToken)"/>
    /// with the case-insensitive options above, so callers don't each need
    /// their own <c>using System.Net.Http.Json;</c> just to reach it.</summary>
    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var value = await client.GetFromJsonAsync<T>(url, Options);
        return value ?? throw new InvalidOperationException($"Response body deserialized to null for {typeof(T).Name}.");
    }
}
