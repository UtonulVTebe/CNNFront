using ExpertAdminTrainerApp.Domain;

namespace ExpertAdminTrainerApp.Services;

public interface IApiClient
{
    // Auth
    Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    // Orders (Expert/Admin)
    Task<IReadOnlyList<OrderAnswerReadDto>> GetQueueAsync(OrderAnswerStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<OrderAnswerReadDto>> GetMineAsync(CancellationToken ct = default);
    Task<PaginatedResponse<OrderAnswerReadDto>> GetOrdersPagedAsync(
        int? cnnId = null, int? studentId = null, int? expertId = null,
        OrderAnswerStatus? status = null, int page = 1, int pageSize = 25,
        CancellationToken ct = default);
    Task<OrderAnswerReadDto> ClaimOrderAsync(int id, CancellationToken ct = default);
    Task<OrderAnswerReadDto> UpdateOrderAsync(int id, OrderAnswerUpdateDto dto, CancellationToken ct = default);
    Task RejectOrderAsync(int id, string reason, CancellationToken ct = default);

    // Reviews
    Task<ReviewReadDto?> GetReviewAsync(int orderAnswerId, CancellationToken ct = default);
    Task<ReviewReadDto> CreateReviewAsync(int orderAnswerId, ReviewWriteDto dto, CancellationToken ct = default);
    Task<ReviewReadDto> UpdateReviewAsync(int orderAnswerId, ReviewWriteDto dto, CancellationToken ct = default);

    // CNN Catalog
    Task<IReadOnlyList<CnnListItemDto>> GetCnnsAsync(CancellationToken ct = default);
    Task<CnnDetailsDto> GetCnnDetailsAsync(int id, CancellationToken ct = default);
    Task<CnnListItemDto> CreateCnnAsync(CnnWriteDto dto, CancellationToken ct = default);
    Task<CnnListItemDto> UpdateCnnAsync(int id, CnnWriteDto dto, CancellationToken ct = default);
    Task DeleteCnnAsync(int id, CancellationToken ct = default);

    // Materials
    Task<CnnMaterialDto> CreateMaterialAsync(int cnnId, CnnMaterialWriteDto dto, CancellationToken ct = default);
    Task<CnnMaterialDto> UpdateMaterialAsync(int cnnId, int materialId, CnnMaterialWriteDto dto, CancellationToken ct = default);
    Task DeleteMaterialAsync(int cnnId, int materialId, CancellationToken ct = default);

    // Files
    Task<FileUploadResponseDto> UploadFileAsync(string filePath, string category, CancellationToken ct = default);

    // Users (Admin)
    Task<PaginatedResponse<UserListItemDto>> GetUsersAsync(string? role = null, string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<UserReadDto> GetUserAsync(int id, CancellationToken ct = default);
    Task<UserReadDto> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default);

    // Expert Info (Admin)
    Task<ExpertInfoReadDto> GetExpertInfoAsync(int userId, CancellationToken ct = default);
    Task<ExpertInfoReadDto> CreateExpertInfoAsync(ExpertInfoCreateDto dto, CancellationToken ct = default);

    // Tools
    Task<bool> ValidateAnswerPayloadAsync(string json, CancellationToken ct = default);
}
