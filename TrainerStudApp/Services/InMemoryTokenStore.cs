namespace TrainerStudApp.Services;

public sealed class InMemoryTokenStore : ITokenStore
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? AccountEmail { get; set; }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = null;
        AccountEmail = null;
    }
}
