namespace ExpertAdminTrainerApp.Services;

public sealed class InMemoryTokenStore : ITokenStore
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
    }
}
