using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TrainerStudApp.Domain;

namespace TrainerStudApp.Services;

public sealed class ApiClient(HttpClient httpClient, ITokenStore tokenStore) : IApiClient
{
    private static readonly TimeSpan AccessExpirySkew = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProactiveRefreshBeforeExpiry = TimeSpan.FromSeconds(90);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/login", ToJsonContent(dto), ct);
        var payload = await ReadAsAsync<LoginResponseDto>(response, ct);
        ApplyLoginResponse(payload, dto.Email);
        return payload;
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken))
            return false;

        try
        {
            await EnsureAccessTokenFreshAsync(ct);
            return !string.IsNullOrWhiteSpace(tokenStore.AccessToken);
        }
        catch
        {
            tokenStore.Clear();
            return false;
        }
    }

    public async Task RegisterRequestCodeAsync(RegisterRequestCodeDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/register/request", ToJsonContent(dto), ct);
        await EnsureSuccessIgnoreBodyAsync(response, ct);
    }

    public async Task<UserReadDto> RegisterConfirmAsync(RegisterConfirmDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/register/confirm", ToJsonContent(dto), ct);
        return await ReadAsAsync<UserReadDto>(response, ct);
    }

    public async Task PasswordResetRequestAsync(PasswordResetRequestCodeDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/password-reset/request", ToJsonContent(dto), ct);
        await EnsureSuccessIgnoreBodyAsync(response, ct);
    }

    public async Task PasswordResetConfirmAsync(PasswordResetConfirmDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/password-reset/confirm", ToJsonContent(dto), ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return;

        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            ThrowFromBody(text, response);
    }

    public Task<IReadOnlyList<CnnListItemDto>> GetCnnsAsync(CancellationToken ct = default) =>
        SendWithAuthAsync<IReadOnlyList<CnnListItemDto>>(HttpMethod.Get, "api/Cnns", null, ct);

    public Task<CnnDetailsDto> GetCnnDetailsAsync(int id, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnDetailsDto>(HttpMethod.Get, $"api/Cnns/{id}/details", null, ct);

    public async Task<string> DownloadTextAsync(string url, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri(url);
        await EnsureAccessTokenFreshAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);

        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
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

    public Uri ResolveToAbsoluteUri(string url) => ResolveRequestUri(url);

    public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct = default)
    {
        var uri = ResolveRequestUri(url);
        await EnsureAccessTokenFreshAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);

        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
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

    public async Task<FileUploadResponseDto> UploadFileAsync(string filePath, string category,
        CancellationToken ct = default)
    {
        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        return await UploadFileBytesAsync(fileBytes, Path.GetFileName(filePath), category, ct);
    }

    private async Task<FileUploadResponseDto> UploadFileBytesAsync(byte[] fileBytes, string fileName, string category,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(category), "category");

        await EnsureAccessTokenFreshAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Post, "api/Files") { Content = form };
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);

        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
        {
            using var form2 = new MultipartFormDataContent();
            var fc2 = new ByteArrayContent(fileBytes);
            fc2.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form2.Add(fc2, "file", fileName);
            form2.Add(new StringContent(category), "category");
            var req2 = new HttpRequestMessage(HttpMethod.Post, "api/Files") { Content = form2 };
            req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
            response = await httpClient.SendAsync(req2, ct);
        }

        return await ReadAsAsync<FileUploadResponseDto>(response, ct);
    }

    public async Task<OrderAnswerReadDto> CreateOrderAnswerAsync(OrderAnswerCreateDto dto, CancellationToken ct = default)
    {
        var response = await SendAuthPostAsync("api/OrderAnswers", dto, ct);
        return await ReadAsAsync<OrderAnswerReadDto>(response, ct);
    }

    public async Task<IReadOnlyList<OrderAnswerReadDto>> GetMyOrderAnswersMineAsync(CancellationToken ct = default)
    {
        var response = await SendAuthGetAsync("api/OrderAnswers/mine", ct);
        var list = await ReadAsAsync<List<OrderAnswerReadDto>>(response, ct);
        return list;
    }

    public async Task<PaginatedResponse<OrderAnswerReadDto>> GetMyOrderAnswersPageAsync(OrderAnswerListQuery query,
        CancellationToken ct = default)
    {
        var url = BuildOrderAnswerListUrl(query);
        var response = await SendAuthGetAsync(url, ct);
        return await ReadAsAsync<PaginatedResponse<OrderAnswerReadDto>>(response, ct);
    }

    public async Task<OrderAnswerReadDto> GetOrderAnswerByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await SendAuthGetAsync($"api/OrderAnswers/{id}", ct);
        return await ReadAsAsync<OrderAnswerReadDto>(response, ct);
    }

    public async Task<OrderAnswerReadDto> UpdateOrderAnswerAsync(int id, OrderAnswerUpdateDto dto, CancellationToken ct = default)
    {
        var response = await SendAuthPutAsync($"api/OrderAnswers/{id}", dto, ct);
        return await ReadAsAsync<OrderAnswerReadDto>(response, ct);
    }

    public async Task<ReviewReadDto> GetOrderAnswerReviewAsync(int orderAnswerId, CancellationToken ct = default)
    {
        var response = await SendAuthGetAsync($"api/orderanswers/{orderAnswerId}/review", ct);
        return await ReadAsAsync<ReviewReadDto>(response, ct);
    }

    private static string BuildOrderAnswerListUrl(OrderAnswerListQuery q)
    {
        var parts = new List<string>
        {
            $"page={Math.Max(1, q.Page)}",
            $"pageSize={Math.Clamp(q.PageSize, 1, 100)}"
        };
        if (q.CnnId is { } cnn)
            parts.Add($"cnnId={cnn}");
        if (q.Status is { } st)
            parts.Add($"status={(int)st}");
        if (q.FromChangedAtUtc is { } from)
            parts.Add("from=" + Uri.EscapeDataString(from.ToUniversalTime().ToString("O")));
        if (q.ToChangedAtUtc is { } to)
            parts.Add("to=" + Uri.EscapeDataString(to.ToUniversalTime().ToString("O")));
        return "api/OrderAnswers?" + string.Join("&", parts);
    }

    private async Task<HttpResponseMessage> SendAuthGetAsync(string relativeUrl, CancellationToken ct)
    {
        await EnsureAccessTokenFreshAsync(ct);
        var response = await SendOnceAsync(HttpMethod.Get, relativeUrl, content: null, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
            response = await SendOnceAsync(HttpMethod.Get, relativeUrl, content: null, ct);
        return response;
    }

    private async Task<HttpResponseMessage> SendAuthPostAsync(string relativeUrl, object body, CancellationToken ct)
    {
        await EnsureAccessTokenFreshAsync(ct);
        var response = await SendOnceAsync(HttpMethod.Post, relativeUrl, ToJsonContent(body), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
            response = await SendOnceAsync(HttpMethod.Post, relativeUrl, ToJsonContent(body), ct);
        return response;
    }

    private async Task<HttpResponseMessage> SendAuthPutAsync(string relativeUrl, object body, CancellationToken ct)
    {
        await EnsureAccessTokenFreshAsync(ct);
        var response = await SendOnceAsync(HttpMethod.Put, relativeUrl, ToJsonContent(body), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
            response = await SendOnceAsync(HttpMethod.Put, relativeUrl, ToJsonContent(body), ct);
        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string relativeUrl, HttpContent? content,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, relativeUrl) { Content = content };
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
        return await httpClient.SendAsync(request, ct);
    }

    private Uri ResolveRequestUri(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute;

        var baseUri = httpClient.BaseAddress
            ?? throw new InvalidOperationException("HttpClient.BaseAddress не задан.");
        return new Uri(baseUri, url.TrimStart('/'));
    }

    private void ApplyLoginResponse(LoginResponseDto payload, string? accountEmail)
    {
        tokenStore.AccessToken = payload.AccessToken;
        tokenStore.RefreshToken = payload.RefreshToken;
        tokenStore.AccessTokenExpiresAtUtc = ComputeAccessExpiryUtc(payload.ExpiresIn);
        if (!string.IsNullOrWhiteSpace(accountEmail))
            tokenStore.AccountEmail = accountEmail.Trim();
    }

    private static DateTime ComputeAccessExpiryUtc(int expiresInSeconds)
    {
        var ttl = TimeSpan.FromSeconds(Math.Max(1, expiresInSeconds));
        return DateTime.UtcNow.Add(ttl) - AccessExpirySkew;
    }

    private bool NeedsProactiveRefresh()
    {
        if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken))
            return false;
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            return true;
        if (tokenStore.AccessTokenExpiresAtUtc is not DateTime exp)
            return true;
        return exp <= DateTime.UtcNow + ProactiveRefreshBeforeExpiry;
    }

    private async Task EnsureAccessTokenFreshAsync(CancellationToken ct)
    {
        if (!NeedsProactiveRefresh())
            return;

        await _refreshGate.WaitAsync(ct);
        try
        {
            if (!NeedsProactiveRefresh())
                return;

            if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken))
                return;

            var response = await httpClient.PostAsync(
                "api/Auth/refresh",
                ToJsonContent(new RefreshTokenRequestDto { RefreshToken = tokenStore.RefreshToken }),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                tokenStore.Clear();
                return;
            }

            var payload = await ReadAsAsync<LoginResponseDto>(response, ct);
            ApplyLoginResponse(payload, accountEmail: null);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<bool> RefreshTokensLockedAsync(CancellationToken ct)
    {
        await _refreshGate.WaitAsync(ct);
        try
        {
            if (string.IsNullOrWhiteSpace(tokenStore.RefreshToken))
                return false;

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
            ApplyLoginResponse(payload, accountEmail: null);
            return true;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<T> SendWithAuthAsync<T>(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        await EnsureAccessTokenFreshAsync(ct);

        var request = BuildRequest(method, url, content, tokenStore.AccessToken);
        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshTokensLockedAsync(ct))
        {
            request = BuildRequest(method, url, content, tokenStore.AccessToken);
            response = await httpClient.SendAsync(request, ct);
        }

        return await ReadAsAsync<T>(response, ct);
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

    private async Task EnsureSuccessIgnoreBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            ThrowFromBody(text, response);
    }

    private async Task<T> ReadAsAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            ThrowFromBody(text, response);

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Empty API payload.");

        var ok = JsonSerializer.Deserialize<T>(text, _jsonOptions);
        if (ok is null) throw new InvalidOperationException("Empty API payload.");
        return ok;
    }

    private static string ParseErrorMessage(string text, JsonSerializerOptions opt)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "HTTP error";

        try
        {
            var simple = JsonSerializer.Deserialize<ApiSimpleMessageDto>(text, opt);
            if (!string.IsNullOrWhiteSpace(simple?.Message))
                return simple.Message;
        }
        catch { /* ignored */ }

        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorDto>(text, opt);
            var m = error?.Message ?? error?.Detail ?? error?.Title;
            if (!string.IsNullOrWhiteSpace(m))
                return m;
        }
        catch { /* ignored */ }

        return text.Length <= 200 ? text : text[..200] + "…";
    }

    private void ThrowFromBody(string text, HttpResponseMessage response)
    {
        var message = ParseErrorMessage(text, _jsonOptions);
        var body = text.Length <= 500 ? text : text[..500] + "…";
        throw new HttpApiException(response.StatusCode, $"{message} | {body}");
    }
}
