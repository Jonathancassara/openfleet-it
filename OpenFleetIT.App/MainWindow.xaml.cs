using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace OpenFleetIT.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ApplicationItem> _applications =
    [
        new("Microsoft 365 Apps", "Microsoft Corporation", "16.0.19127.20264", "À jour", "#2457D7A3", "#FF57D7A3"),
        new("Google Chrome", "Google LLC", "140.0.7339.81", "Mise à jour", "#24FFBC66", "#FFFFBC66"),
        new("7-Zip", "Igor Pavlov", "24.09", "À jour", "#2457D7A3", "#FF57D7A3"),
        new("VLC media player", "VideoLAN", "3.0.21", "Réparation", "#248B7CFF", "#FFA99CFF"),
        new("Microsoft Teams", "Microsoft Corporation", "25193.1707.3773.5286", "À jour", "#2457D7A3", "#FF57D7A3"),
        new("Adobe Acrobat Reader", "Adobe", "25.001.20672", "Mise à jour", "#24FFBC66", "#FFFFBC66")
    ];

    public ICollectionView ApplicationsView { get; }

    public MainWindow()
    {
        InitializeComponent();
        ApplicationsView = CollectionViewSource.GetDefaultView(_applications);
        DataContext = this;
        SourceInitialized += (_, _) => EnableSystemBackdrop();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ApplicationsView is null) return;

        var query = SearchBox.Text.Trim();
        ApplicationsView.Filter = item =>
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (item is not ApplicationItem application) return false;
            return application.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || application.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || application.Version.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || application.Status.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void AppAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not ApplicationItem application)
            return;

        var action = button.Tag?.ToString() switch
        {
            "Repair" => "Réparer",
            "Update" => "Mettre à jour",
            "Uninstall" => "Désinstaller",
            _ => "Exécuter une action sur"
        };

        MessageBox.Show(
            $"Prototype sécurisé : l’action « {action} » pour {application.Name} sera ajoutée au moteur d’exécution lors de la prochaine étape.\n\nAucune modification n’a été faite sur ce poste.",
            "OpenFleet IT · Action simulée",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void EnableSystemBackdrop()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        const int darkModeAttribute = 20;
        const int systemBackdropAttribute = 38;
        var enabled = 1;
        var backdropType = 2;
        _ = DwmSetWindowAttribute(hwnd, darkModeAttribute, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, systemBackdropAttribute, ref backdropType, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}

public sealed record ApplicationItem(string Name, string Publisher, string Version, string Status,
    string StatusBackgroundHex, string StatusForegroundHex)
{
    public Brush StatusBackground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusBackgroundHex));
    public Brush StatusForeground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusForegroundHex));
}
