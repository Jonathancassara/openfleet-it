using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace OpenFleetIT.App;

public partial class SettingsWindow : Window
{
    private static readonly Regex SuffixPattern = new(@"^\.(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,63}$", RegexOptions.Compiled);
    private readonly ObservableCollection<string> _suffixes = [];
    private string _selectedLanguage = LocalizationService.English;
    private bool _isLoaded;

    public SettingsWindow()
    {
        InitializeComponent();
        SuffixList.ItemsSource = _suffixes;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
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
        SaveStatus.Text = LocalizationService.Text("SettingsSaved");
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus.Text = LocalizationService.Text("CheckingUpdates");
        await Task.Delay(650);
        UpdateStatus.Text = LocalizationService.Text("NoUpdates");
    }

    private void LanguageSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isLoaded || LanguageSelector.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        _selectedLanguage = item.Tag?.ToString() == LocalizationService.French ? LocalizationService.French : LocalizationService.English;
        LocalizationService.Apply(_selectedLanguage);
        UpdateStatus.Text = LocalizationService.Text("CurrentVersion");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void EnableDarkTitleBar()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        const int darkModeAttribute = 20;
        var enabled = 1;
        _ = DwmSetWindowAttribute(hwnd, darkModeAttribute, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
