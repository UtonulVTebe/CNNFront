using TrainerStudApp.Domain;

namespace TrainerStudApp.Services;

public interface IApiClient
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<CnnListItemDto>> GetCnnsAsync(CancellationToken ct = default);

    Task<CnnDetailsDto> GetCnnDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>GET текста по абсолютному или относительному URL (Bearer при наличии токена).</summary>
    Task<string> DownloadTextAsync(string url, CancellationToken ct = default);

    /// <summary>GET бинарных данных (КИМ, изображения бланков) с Bearer при необходимости.</summary>
    Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct = default);
}
