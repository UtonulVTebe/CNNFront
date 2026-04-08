namespace TrainerStudApp.Services;

public interface ITokenStore
{
    string? AccessToken { get; set; }
    string? RefreshToken { get; set; }

    /// <summary>UTC: момент, после которого access считается просроченным (уже с запасом относительно ExpiresIn).</summary>
    DateTime? AccessTokenExpiresAtUtc { get; set; }

    /// <summary>Email для подписи в UI после перезапуска.</summary>
    string? AccountEmail { get; set; }

    void Clear();
}
