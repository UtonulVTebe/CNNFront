using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Services;

public sealed class ApiClient(HttpClient httpClient, ITokenStore tokenStore) : IApiClient
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ===== Auth =====

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync("api/Auth/login", ToJsonContent(dto), ct);
        var payload = await ReadAsAsync<LoginResponseDto>(response, ct);
        tokenStore.AccessToken = payload.AccessToken;
        tokenStore.RefreshToken = payload.RefreshToken;
        return payload;
    }

    // ===== Orders =====

    public Task<IReadOnlyList<OrderAnswerReadDto>> GetQueueAsync(OrderAnswerStatus? status, CancellationToken ct = default)
    {
        var suffix = status is null ? string.Empty : $"?status={(int)status.Value}";
        return SendWithAuthAsync<IReadOnlyList<OrderAnswerReadDto>>(HttpMethod.Get, $"api/OrderAnswers/queue{suffix}", null, ct);
    }

    public Task<IReadOnlyList<OrderAnswerReadDto>> GetMineAsync(CancellationToken ct = default) =>
        SendWithAuthAsync<IReadOnlyList<OrderAnswerReadDto>>(HttpMethod.Get, "api/OrderAnswers/mine", null, ct);

    public Task<PaginatedResponse<OrderAnswerReadDto>> GetOrdersPagedAsync(
        int? cnnId = null, int? studentId = null, int? expertId = null,
        OrderAnswerStatus? status = null, int page = 1, int pageSize = 25,
        CancellationToken ct = default)
    {
        var qs = BuildQueryString(
            ("cnnId", cnnId?.ToString()),
            ("studentId", studentId?.ToString()),
            ("expertId", expertId?.ToString()),
            ("status", status is null ? null : ((int)status.Value).ToString()),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        return SendWithAuthAsync<PaginatedResponse<OrderAnswerReadDto>>(HttpMethod.Get, $"api/OrderAnswers{qs}", null, ct);
    }

    public Task<OrderAnswerReadDto> ClaimOrderAsync(int id, CancellationToken ct = default) =>
        SendWithAuthAsync<OrderAnswerReadDto>(HttpMethod.Post, $"api/OrderAnswers/{id}/claim", null, ct);

    public Task<OrderAnswerReadDto> UpdateOrderAsync(int id, OrderAnswerUpdateDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<OrderAnswerReadDto>(HttpMethod.Put, $"api/OrderAnswers/{id}", ToJsonContent(dto), ct);

    public Task RejectOrderAsync(int id, string reason, CancellationToken ct = default) =>
        SendNoContentWithAuthAsync(HttpMethod.Post, $"api/OrderAnswers/{id}/reject",
            ToJsonContent(new OrderAnswerRejectDto { Reason = reason }), ct);

    // ===== Reviews =====

    public async Task<ReviewReadDto?> GetReviewAsync(int orderAnswerId, CancellationToken ct = default)
    {
        try
        {
            return await SendWithAuthAsync<ReviewReadDto>(HttpMethod.Get, $"api/orderanswers/{orderAnswerId}/review", null, ct);
        }
        catch
        {
            return null;
        }
    }

    public Task<ReviewReadDto> CreateReviewAsync(int orderAnswerId, ReviewWriteDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<ReviewReadDto>(HttpMethod.Post, $"api/orderanswers/{orderAnswerId}/review", ToJsonContent(dto), ct);

    public Task<ReviewReadDto> UpdateReviewAsync(int orderAnswerId, ReviewWriteDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<ReviewReadDto>(HttpMethod.Put, $"api/orderanswers/{orderAnswerId}/review", ToJsonContent(dto), ct);

    // ===== CNN =====

    public Task<IReadOnlyList<CnnListItemDto>> GetCnnsAsync(CancellationToken ct = default) =>
        SendWithAuthAsync<IReadOnlyList<CnnListItemDto>>(HttpMethod.Get, "api/Cnns", null, ct);

    public Task<CnnDetailsDto> GetCnnDetailsAsync(int id, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnDetailsDto>(HttpMethod.Get, $"api/Cnns/{id}/details", null, ct);

    public Task<CnnListItemDto> CreateCnnAsync(CnnWriteDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnListItemDto>(HttpMethod.Post, "api/Cnns", ToJsonContent(dto), ct);

    public Task<CnnListItemDto> UpdateCnnAsync(int id, CnnWriteDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnListItemDto>(HttpMethod.Put, $"api/Cnns/{id}", ToJsonContent(dto), ct);

    public Task DeleteCnnAsync(int id, CancellationToken ct = default) =>
        SendNoContentWithAuthAsync(HttpMethod.Delete, $"api/Cnns/{id}", null, ct);

    // ===== Materials =====

    public Task<CnnMaterialDto> CreateMaterialAsync(int cnnId, CnnMaterialWriteDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnMaterialDto>(HttpMethod.Post, $"api/Cnns/{cnnId}/materials", ToJsonContent(dto), ct);

    public Task<CnnMaterialDto> UpdateMaterialAsync(int cnnId, int materialId, CnnMaterialWriteDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<CnnMaterialDto>(HttpMethod.Put, $"api/Cnns/{cnnId}/materials/{materialId}", ToJsonContent(dto), ct);

    public Task DeleteMaterialAsync(int cnnId, int materialId, CancellationToken ct = default) =>
        SendNoContentWithAuthAsync(HttpMethod.Delete, $"api/Cnns/{cnnId}/materials/{materialId}", null, ct);

    // ===== Files =====

    public async Task<FileUploadResponseDto> UploadFileAsync(string filePath, string category, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        form.Add(new StringContent(category), "category");

        var request = new HttpRequestMessage(HttpMethod.Post, "api/Files") { Content = form };
        if (!string.IsNullOrWhiteSpace(tokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);

        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
        {
            using var form2 = new MultipartFormDataContent();
            var fc2 = new ByteArrayContent(fileBytes);
            fc2.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form2.Add(fc2, "file", Path.GetFileName(filePath));
            form2.Add(new StringContent(category), "category");
            var req2 = new HttpRequestMessage(HttpMethod.Post, "api/Files") { Content = form2 };
            req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
            response = await httpClient.SendAsync(req2, ct);
        }

        return await ReadAsAsync<FileUploadResponseDto>(response, ct);
    }

    // ===== Users =====

    public Task<PaginatedResponse<UserListItemDto>> GetUsersAsync(
        string? role = null, string? search = null, int page = 1, int pageSize = 25,
        CancellationToken ct = default)
    {
        var qs = BuildQueryString(
            ("role", role),
            ("search", search),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));
        return SendWithAuthAsync<PaginatedResponse<UserListItemDto>>(HttpMethod.Get, $"api/Users{qs}", null, ct);
    }

    public Task<UserReadDto> GetUserAsync(int id, CancellationToken ct = default) =>
        SendWithAuthAsync<UserReadDto>(HttpMethod.Get, $"api/Users/{id}", null, ct);

    public Task<UserReadDto> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<UserReadDto>(HttpMethod.Put, $"api/Users/{id}", ToJsonContent(dto), ct);

    // ===== ExpertInfo =====

    public Task<ExpertInfoReadDto> GetExpertInfoAsync(int userId, CancellationToken ct = default) =>
        SendWithAuthAsync<ExpertInfoReadDto>(HttpMethod.Get, $"api/ExpertInfos/{userId}", null, ct);

    public Task<ExpertInfoReadDto> CreateExpertInfoAsync(ExpertInfoCreateDto dto, CancellationToken ct = default) =>
        SendWithAuthAsync<ExpertInfoReadDto>(HttpMethod.Post, "api/ExpertInfos", ToJsonContent(dto), ct);

    // ===== Tools =====

    public async Task<bool> ValidateAnswerPayloadAsync(string json, CancellationToken ct = default)
    {
        var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("api/AnswerPayload/validate", body, ct);
        return response.IsSuccessStatusCode;
    }

    // ===== Infrastructure =====

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

    private async Task SendNoContentWithAuthAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        var request = BuildRequest(method, url, content, tokenStore.AccessToken);
        var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
        {
            request = BuildRequest(method, url, content, tokenStore.AccessToken);
            response = await httpClient.SendAsync(request, ct);
        }

        if (!response.IsSuccessStatusCode)
            await ReadAsAsync<ApiErrorDto>(response, ct);
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
        if (response.IsSuccessStatusCode)
        {
            var ok = JsonSerializer.Deserialize<T>(text, _jsonOptions);
            if (ok is null) throw new InvalidOperationException("Empty API payload.");
            return ok;
        }

        ApiErrorDto? error = null;
        try { error = JsonSerializer.Deserialize<ApiErrorDto>(text, _jsonOptions); }
        catch { /* ignored */ }

        var message = error?.Message ?? error?.Detail ?? error?.Title
            ?? $"HTTP {(int)response.StatusCode}";

        // Include raw body for easier diagnostics when nothing else is available
        var body = text.Length <= 500 ? text : text[..500] + "…";
        throw new InvalidOperationException($"{message} | {body}");
    }

    private static string BuildQueryString(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : string.Empty;
    }
}
