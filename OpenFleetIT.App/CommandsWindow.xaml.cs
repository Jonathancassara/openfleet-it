using System.Windows;

namespace OpenFleetIT.App;

public partial class CommandsWindow : Window
{
    public CommandsWindow(string? connectedTarget)
    {
        InitializeComponent();
        TargetLabel.Text = string.IsNullOrWhiteSpace(connectedTarget)
            ? LocalizationService.Text("NoConnectedDevice")
            : connectedTarget;
        NoticeLabel.Text = LocalizationService.Text("CommandSafetyNotice");
    }
}
