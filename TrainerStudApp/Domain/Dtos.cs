using System.Text.Json.Serialization;

namespace TrainerStudApp.Domain;

// ===== Auth =====

public sealed class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public int RefreshExpiresIn { get; set; }
}

public sealed class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RegisterRequestCodeDto
{
    public string Email { get; set; } = string.Empty;
}

public sealed class RegisterConfirmDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class PasswordResetRequestCodeDto
{
    public string Email { get; set; } = string.Empty;
}

public sealed class PasswordResetConfirmDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

// ===== Users =====

public sealed class UserReadDto
{
    public int Id { get; set; }
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class UserListItemDto
{
    public int Id { get; set; }
    public string? Role { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int? Balance { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }

    [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }

    [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
}

public sealed class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// ===== ExpertInfo =====

public sealed class ExpertInfoReadDto
{
    public int UserId { get; set; }
    public int Balance { get; set; }
}

public sealed class ExpertInfoCreateDto
{
    public int UserId { get; set; }
    public int InitialBalance { get; set; }
}

// ===== CNN =====

public sealed class CnnListItemDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public int Option { get; set; }
}

public sealed class CnnWriteDto
{
    public string Subject { get; set; } = string.Empty;
    public int Option { get; set; }
}

public sealed class CnnMaterialDto
{
    public int Id { get; set; }
    public MaterialKind Kind { get; set; }
    public string? Title { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class CnnMaterialWriteDto
{
    public MaterialKind Kind { get; set; } = MaterialKind.Other;
    public string? Title { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class CnnDetailsDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public int Option { get; set; }
    public List<CnnMaterialDto> Materials { get; set; } = [];
}

// ===== Annotation Zones =====

public sealed class AnnotationZoneReadDto
{
    public int Id { get; set; }
    public int CnnMaterialId { get; set; }
    public int TaskNumber { get; set; }
    public AnnotationFieldType FieldType { get; set; }
    public int Page { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string? Label { get; set; }
}

public sealed class AnnotationZoneWriteDto
{
    public int TaskNumber { get; set; }
    public AnnotationFieldType FieldType { get; set; }
    public int Page { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string? Label { get; set; }
}

// ===== Orders (orderanswers API) =====

public sealed class OrderAnswerCreateDto
{
    public int CnnId { get; set; }
    public string AnswerUrl { get; set; } = string.Empty;
    public OrderAnswerStatus? Status { get; set; }
}

public sealed class OrderAnswerReadDto
{
    public int Id { get; set; }
    public int CnnId { get; set; }
    public int UserId { get; set; }
    public int? ExpertId { get; set; }
    public string? AnswerUrl { get; set; }
    public OrderAnswerStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class OrderAnswerUpdateDto
{
    public string? AnswerUrl { get; set; }
    public OrderAnswerStatus? Status { get; set; }
    public int? ExpertId { get; set; }
}

/// <summary>Query для GET <c>/api/orderanswers</c> (пагинация и фильтры).</summary>
public sealed class OrderAnswerListQuery
{
    public int? CnnId { get; set; }
    public OrderAnswerStatus? Status { get; set; }
    public DateTime? FromChangedAtUtc { get; set; }
    public DateTime? ToChangedAtUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class OrderAnswerRejectDto
{
    public string Reason { get; set; } = string.Empty;
}

// ===== Review =====

public sealed class ReviewCriterionDto
{
    public int TaskNumber { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? Comment { get; set; }
}

public sealed class ReviewReadDto
{
    public int Id { get; set; }
    public int OrderAnswerId { get; set; }
    public int ExpertId { get; set; }
    public int TotalScore { get; set; }
    public string? GeneralComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ReviewCriterionDto> Criteria { get; set; } = [];
}

public sealed class ReviewWriteDto
{
    public int TotalScore { get; set; }
    public string? GeneralComment { get; set; }
    public List<ReviewCriterionDto> Criteria { get; set; } = [];
}

// ===== Files =====

public sealed class FileUploadResponseDto
{
    public string? Url { get; set; }
    public string? FileName { get; set; }
    public long SizeBytes { get; set; }
}

// ===== Transactions =====

public sealed class TransactionReadDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? OrderAnswerId { get; set; }
    public int Sum { get; set; }
    public PlatformTransactionType Type { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }
}

// ===== Errors =====

public sealed class ApiErrorDto
{
    public string? Message { get; set; }
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

/// <summary>Частый формат ошибок ASP.NET: <c>{"message":"..."}</c>.</summary>
public sealed class ApiSimpleMessageDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
