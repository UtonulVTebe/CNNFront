namespace ExpertAdminTrainerApp.Services;

public interface ITokenStore
{
    string? AccessToken { get; set; }
    string? RefreshToken { get; set; }
    void Clear();
}
