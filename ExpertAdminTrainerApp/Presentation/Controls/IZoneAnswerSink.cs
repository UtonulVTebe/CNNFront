namespace ExpertAdminTrainerApp.Presentation.Controls;

public interface IZoneAnswerSink
{
    string? GetAnswer(string key);

    void SetAnswer(string key, string? value);
}
