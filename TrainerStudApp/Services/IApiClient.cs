using TrainerStudApp.Domain;

namespace TrainerStudApp.Services;

public interface IApiClient
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>Восстановить access по refresh (или подтвердить, что access ещё валиден).</summary>
    Task<bool> TryRestoreSessionAsync(CancellationToken ct = default);

    Task RegisterRequestCodeAsync(RegisterRequestCodeDto dto, CancellationToken ct = default);

    Task<UserReadDto> RegisterConfirmAsync(RegisterConfirmDto dto, CancellationToken ct = default);

    Task PasswordResetRequestAsync(PasswordResetRequestCodeDto dto, CancellationToken ct = default);

    Task PasswordResetConfirmAsync(PasswordResetConfirmDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<CnnListItemDto>> GetCnnsAsync(CancellationToken ct = default);

    Task<CnnDetailsDto> GetCnnDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>GET текста по абсолютному или относительному URL (Bearer при наличии токена).</summary>
    Task<string> DownloadTextAsync(string url, CancellationToken ct = default);

    /// <summary>GET бинарных данных (КИМ, изображения бланков) с Bearer при необходимости.</summary>
    Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct = default);

    /// <summary>Абсолютный URI для относительных путей API (как в запросах HttpClient).</summary>
    Uri ResolveToAbsoluteUri(string url);

    // ----- Order answers (student) -----

    Task<OrderAnswerReadDto> CreateOrderAnswerAsync(OrderAnswerCreateDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<OrderAnswerReadDto>> GetMyOrderAnswersMineAsync(CancellationToken ct = default);

    Task<PaginatedResponse<OrderAnswerReadDto>> GetMyOrderAnswersPageAsync(OrderAnswerListQuery query,
        CancellationToken ct = default);

    Task<OrderAnswerReadDto> GetOrderAnswerByIdAsync(int id, CancellationToken ct = default);

    Task<OrderAnswerReadDto> UpdateOrderAnswerAsync(int id, OrderAnswerUpdateDto dto, CancellationToken ct = default);

    /// <summary>GET review; при 404 NotFound — <see cref="HttpApiException"/>.</summary>
    Task<ReviewReadDto> GetOrderAnswerReviewAsync(int orderAnswerId, CancellationToken ct = default);
}
