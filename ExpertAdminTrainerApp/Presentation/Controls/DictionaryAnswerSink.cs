namespace ExpertAdminTrainerApp.Presentation.Controls;

public sealed class DictionaryAnswerSink(Dictionary<string, string> answers) : IZoneAnswerSink
{
    public string? GetAnswer(string key) =>
        answers.TryGetValue(key, out var v) ? v : null;

    public void SetAnswer(string key, string? value)
    {
        /* read-only preview */
    }
}
