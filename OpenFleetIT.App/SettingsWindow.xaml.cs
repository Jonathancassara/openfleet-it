using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;

namespace OpenFleetIT.App;

public partial class SettingsWindow : Window
{
    private static readonly Regex SuffixPattern = new(@"^\.(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,63}$", RegexOptions.Compiled);
    private readonly ObservableCollection<string> _suffixes = [];

    public SettingsWindow()
    {
        InitializeComponent();
        SuffixList.ItemsSource = _suffixes;
        Loaded += async (_, _) =>
        {
            var settings = await SettingsStore.LoadAsync();
            foreach (var suffix in settings.DomainSuffixes.Distinct(StringComparer.OrdinalIgnoreCase))
                _suffixes.Add(suffix);
        };
    }

    private void AddSuffix_Click(object sender, RoutedEventArgs e)
    {
        var suffix = SuffixInput.Text.Trim().ToLowerInvariant();
        if (!SuffixPattern.IsMatch(suffix))
        {
            MessageBox.Show("Saisissez un suffixe DNS valide, par exemple .entreprise.fr.", "Suffixe invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_suffixes.Contains(suffix, StringComparer.OrdinalIgnoreCase)) return;
        _suffixes.Add(suffix);
        SuffixInput.Clear();
    }

    private void RemoveSuffix_Click(object sender, RoutedEventArgs e)
    {
        if (SuffixList.SelectedItem is string suffix) _suffixes.Remove(suffix);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await SettingsStore.SaveAsync(new OpenFleetSettings { DomainSuffixes = [.. _suffixes] });
        SaveStatus.Text = "Paramètres enregistrés localement.";
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus.Text = "Vérification en cours…";
        await Task.Delay(650);
        UpdateStatus.Text = "Version 0.1.0-preview · aucune mise à jour disponible (mode démonstration).";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
