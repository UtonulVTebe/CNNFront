namespace ExpertAdminTrainerApp.Domain;

/// <summary>Страница бланка №2 в пакете ответа для превью эксперта; <see cref="TemplatePageIndex"/> — индекс в полном списке <c>template.pages</c> (ключи answers).</summary>
public sealed class OrderExpertSheet2PageItem
{
    public OrderExpertSheet2PageItem(BlankPageDefinition page, int templatePageIndex)
    {
        Page = page;
        TemplatePageIndex = templatePageIndex;
    }

    public BlankPageDefinition Page { get; }
    public int TemplatePageIndex { get; }
    public string DisplayLabel => $"Бланк №2 · стр. {Page.PageNumber}";
}
