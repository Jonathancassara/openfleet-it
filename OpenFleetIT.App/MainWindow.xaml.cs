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
    private readonly ObservableCollection<DeviceItem> _devices =
    [
        new("OF-LT-0248", "Surface Laptop 6", "Alice Martin", "Windows 11 24H2", "Conforme", "Il y a 2 min", "#2457D7A3", "#FF57D7A3"),
        new("OF-WS-0187", "Dell Precision 3680", "Marc Leroy", "Windows 11 24H2", "Conforme", "Il y a 5 min", "#2457D7A3", "#FF57D7A3"),
        new("OF-LT-0314", "ThinkPad T14 Gen 5", "Sofia Benali", "Windows 11 23H2", "À surveiller", "Il y a 12 min", "#24FFBC66", "#FFFFBC66"),
        new("OF-MB-0042", "MacBook Pro 14", "Thomas Robert", "macOS 15.5", "Conforme", "Il y a 19 min", "#2457D7A3", "#FF57D7A3"),
        new("OF-LT-0098", "HP EliteBook 840", "Camille Dupont", "Windows 10 22H2", "Critique", "Il y a 31 min", "#24FF7188", "#FFFF7188"),
        new("OF-PH-0116", "iPhone 16 Pro", "Nicolas Petit", "iOS 19.0", "Conforme", "Il y a 44 min", "#2457D7A3", "#FF57D7A3")
    ];

    public ICollectionView DevicesView { get; }

    public MainWindow()
    {
        InitializeComponent();
        DevicesView = CollectionViewSource.GetDefaultView(_devices);
        DataContext = this;
        SourceInitialized += (_, _) => EnableSystemBackdrop();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (DevicesView is null) return;

        var query = SearchBox.Text.Trim();
        DevicesView.Filter = item =>
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (item is not DeviceItem device) return false;
            return device.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || device.Model.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || device.User.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || device.OperatingSystem.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || device.Status.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
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

public sealed record DeviceItem(string Name, string Model, string User, string OperatingSystem, string Status,
    string LastSeen, string StatusBackgroundHex, string StatusForegroundHex)
{
    public Brush StatusBackground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusBackgroundHex));
    public Brush StatusForeground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusForegroundHex));
}
