using Lexon.AI;

namespace Lexon.SettingsModel;

public static class AiModelChoices
{
    public static IReadOnlyList<string> ForProvider(string provider, string? selected)
    {
        var models = AiProviderCatalog.Models(provider).ToList();
        if (models.Count == 0)
        {
            return models;
        }

        var pick = string.IsNullOrWhiteSpace(selected)
            ? AiProviderCatalog.DefaultModel(provider)
            : selected;
        if (!models.Contains(pick))
        {
            models.Insert(0, pick);
        }

        return models;
    }

    public static string ResolveSelected(string provider, string? comboValue)
        => comboValue ?? AiProviderCatalog.DefaultModel(provider);

    public static string ResolveUiProvider(bool advancedVisible, string? comboSelection)
        => advancedVisible
            ? comboSelection ?? AiProviderCatalog.Recommended
            : AiProviderCatalog.Recommended;
}
