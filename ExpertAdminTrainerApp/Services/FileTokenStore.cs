using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace ExpertAdminTrainerApp.Services;

public sealed class FileTokenStore : ITokenStore
{
    private readonly string _filePath;
    private string? _accessToken;
    private string? _refreshToken;

    public FileTokenStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "ExpertAdminTrainerApp");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "session.dat");
        LoadFromDisk();
    }

    public string? AccessToken
    {
        get => _accessToken;
        set
        {
            _accessToken = value;
            SaveToDisk();
        }
    }

    public string? RefreshToken
    {
        get => _refreshToken;
        set
        {
            _refreshToken = value;
            SaveToDisk();
        }
    }

    public void Clear()
    {
        _accessToken = null;
        _refreshToken = null;
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var encrypted = File.ReadAllBytes(_filePath);
            var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(data);
            var payload = JsonSerializer.Deserialize<TokenPayload>(json);
            _accessToken = payload?.AccessToken;
            _refreshToken = payload?.RefreshToken;
        }
        catch
        {
            Clear();
        }
    }

    private void SaveToDisk()
    {
        var payload = new TokenPayload
        {
            AccessToken = _accessToken,
            RefreshToken = _refreshToken
        };

        var json = JsonSerializer.Serialize(payload);
        var raw = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
    }

    private sealed class TokenPayload
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }
}
