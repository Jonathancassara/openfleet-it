using System.Windows;

namespace OpenFleetIT.App;

public static class LocalizationService
{
    public const string English = "en";
    public const string French = "fr";

    public static string CurrentLanguage { get; private set; } = English;

    public static void Apply(string? language)
    {
        CurrentLanguage = language == French ? French : English;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(item => item.Source?.OriginalString.Contains("Strings.") == true);
        if (existing is not null) dictionaries.Remove(existing);
        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{CurrentLanguage}.xaml", UriKind.Relative)
        });
    }

    public static string Text(string key) => Application.Current.TryFindResource(key)?.ToString() ?? key;
}
