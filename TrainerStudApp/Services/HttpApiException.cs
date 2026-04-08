using System.Net;

namespace TrainerStudApp.Services;

/// <summary>Ответ API с известным HTTP-кодом и сообщением из тела (message / ProblemDetails).</summary>
public sealed class HttpApiException : InvalidOperationException
{
    public HttpStatusCode StatusCode { get; }

    public HttpApiException(HttpStatusCode statusCode, string message) : base(message) =>
        StatusCode = statusCode;
}
