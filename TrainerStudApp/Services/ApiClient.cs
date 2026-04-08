using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TrainerStudApp.Domain;

namespace TrainerStudApp.Services;

public sealed class ApiClient(HttpClient httpClient, ITokenStore tokenStore) : IApiClient
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/login", ToJsonContent(dto), ct);
        var payload = await ReadAsAsync<LoginResponseDto>(response, ct);
        tokenStore.AccessToken = payload.AccessToken;
        tokenStore.RefreshToken = payload.RefreshToken;
        return payload;
    }

    public Task<IReadOnlyList<CnnListItemDto>> GetCnnsAsync(CancellationToken ct = default) =>
        SendWithAuthAsync<IReadOnlyList<CnnListItemDto>>(HttpMethod.Get, "api/Cnns", null, ct);

    public Task<CnnDetailsDto> GetCnnDetailsAsync(int id, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnDetailsDto>(HttpMethod.Get, $"api/Cnns/{id}/details", null, ct);

    public async Task<string> DownloadTextAsync(string url, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri(url);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);

        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
        {
            request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
            response = await httpClient.SendAsync(request, ct);
        }

        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            ThrowFromBody(text, response);

        return text;
    }

    public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri(url);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);

        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
        {
            request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
            response = await httpClient.SendAsync(request, ct);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var text = Encoding.UTF8.GetString(bytes);
            ThrowFromBody(text, response);
        }

        return bytes;
    }

    private Uri ResolveRequestUri(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute;

        var baseUri = httpClient.BaseAddress
            ?? throw new InvalidOperationException("HttpClient.BaseAddress не задан.");
        return new Uri(baseUri, url.TrimStart('/'));
    }

    private async Task<T> SendWithAuthAsync<T>(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        var request = BuildRequest(method, url, content, tokenStore.AccessToken);
        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
        {
            request = BuildRequest(method, url, content, tokenStore.AccessToken);
            response = await httpClient.SendAsync(request, ct);
        }

        return await ReadAsAsync<T>(response, ct);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken)) return false;

        var response = await httpClient.PostAsync(
            "api/Auth/refresh",
            ToJsonContent(new RefreshTokenRequestDto { RefreshToken = tokenStore.RefreshToken }),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            tokenStore.Clear();
            return false;
        }

        var payload = await ReadAsAsync<LoginResponseDto>(response, ct);
        tokenStore.AccessToken = payload.AccessToken;
        tokenStore.RefreshToken = payload.RefreshToken;
        return true;
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, HttpContent? content, string? accessToken)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private StringContent ToJsonContent<T>(T body) =>
        new(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

    private async Task<T> ReadAsAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            ThrowFromBody(text, response);

        var ok = JsonSerializer.Deserialize<T>(text, _jsonOptions);
        if (ok is null) throw new InvalidOperationException("Empty API payload.");
        return ok;
    }

    private void ThrowFromBody(string text, HttpResponseMessage response)
    {
        ApiErrorDto? error = null;
        try { error = JsonSerializer.Deserialize<ApiErrorDto>(text, _jsonOptions); }
        catch { /* ignored */ }

        var message = error?.Message ?? error?.Detail ?? error?.Title
            ?? $"HTTP {(int)response.StatusCode}";
        var body = text.Length <= 500 ? text : text[..500] + "…";
        throw new InvalidOperationException($"{message} | {body}");
    }
}
