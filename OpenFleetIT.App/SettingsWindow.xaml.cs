using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace OpenFleetIT.App;

public partial class SettingsWindow : UserControl
{
    public event EventHandler? LanguageChanged;
    private static readonly Regex SuffixPattern = new(@"^\.(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,63}$", RegexOptions.Compiled);
    private readonly ObservableCollection<string> _suffixes = [];
    private string _selectedLanguage = LocalizationService.English;
    private bool _isLoaded;

    public SettingsWindow()
    {
        InitializeComponent();
        SuffixList.ItemsSource = _suffixes;
        Loaded += async (_, _) =>
        {
            var settings = await SettingsStore.LoadAsync();
            _selectedLanguage = settings.Language == LocalizationService.French ? LocalizationService.French : LocalizationService.English;
            LanguageSelector.SelectedIndex = _selectedLanguage == LocalizationService.French ? 1 : 0;
            foreach (var suffix in settings.DomainSuffixes.Distinct(StringComparer.OrdinalIgnoreCase))
                _suffixes.Add(suffix);
            _isLoaded = true;
        };
    }

    private void AddSuffix_Click(object sender, RoutedEventArgs e)
    {
        var suffix = SuffixInput.Text.Trim().ToLowerInvariant();
        if (!SuffixPattern.IsMatch(suffix))
        {
            MessageBox.Show(LocalizationService.Text("InvalidSuffixMessage"), LocalizationService.Text("InvalidSuffixTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
        await SettingsStore.SaveAsync(new OpenFleetSettings { DomainSuffixes = [.. _suffixes], Language = _selectedLanguage });
        await ActionLogService.AppendAsync("Configuration", "localhost", "Save settings", "Success",
            $"Language={_selectedLanguage}; DnsSuffixCount={_suffixes.Count}");
        SaveStatus.Text = LocalizationService.Text("SettingsSaved");
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus.Text = LocalizationService.Text("CheckingUpdates");
        var result = await OpenFleetUpdateService.CheckAsync();
        UpdateStatus.Text = result.Error is not null
            ? string.Format(LocalizationService.Text("UpdateCheckErrorFormat"), result.Error)
            : result.UpdateAvailable
                ? string.Format(LocalizationService.Text("OpenFleetUpdateAvailableFormat"), result.LatestVersion)
                : LocalizationService.Text("NoUpdates");
        await ActionLogService.AppendAsync("Update", "OpenFleet IT", "Check application update",
            result.Error is null ? "Success" : "Error", result.Error ?? $"Latest={result.LatestVersion ?? OpenFleetUpdateService.CurrentVersion}");
    }

    private void LanguageSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isLoaded || LanguageSelector.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        _selectedLanguage = item.Tag?.ToString() == LocalizationService.French ? LocalizationService.French : LocalizationService.English;
        LocalizationService.Apply(_selectedLanguage);
        UpdateStatus.Text = LocalizationService.Text("CurrentVersion");
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

}
