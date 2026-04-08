namespace ExpertAdminTrainerApp.Services;

/// <summary>Ошибка HTTP API с текстом, пригодным для показа пользователю (без сырого тела ответа).</summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
