using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TextLayer.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly Action captureRegionAction;
    private readonly Action captureActiveWindowAction;
    private readonly Action openAction;
    private readonly Action openImageAction;
    private readonly Action settingsAction;
    private readonly Action aboutAction;
    private readonly Action exitAction;

    public TrayIconService(
        Icon icon,
        Action captureRegionAction,
        Action captureActiveWindowAction,
        Action openAction,
        Action openImageAction,
        Action settingsAction,
        Action aboutAction,
        Action exitAction)
    {
        this.captureRegionAction = captureRegionAction;
        this.captureActiveWindowAction = captureActiveWindowAction;
        this.openAction = openAction;
        this.openImageAction = openImageAction;
        this.settingsAction = settingsAction;
        this.aboutAction = aboutAction;
        this.exitAction = exitAction;

        notifyIcon = new NotifyIcon
        {
            Text = UiTextService.Instance["App.Name"],
            Icon = icon,
            Visible = true,
        };

        RebuildMenu();
        notifyIcon.DoubleClick += (_, _) => openAction();
        UiTextService.Instance.PropertyChanged += UiTextService_OnPropertyChanged;
    }

    public void Dispose()
    {
        UiTextService.Instance.PropertyChanged -= UiTextService_OnPropertyChanged;
        notifyIcon.ContextMenuStrip?.Dispose();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.None)
        => notifyIcon.ShowBalloonTip(2500, title, text, icon);

    private void UiTextService_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not "Item[]" and not nameof(UiTextService.CurrentLanguage))
        {
            return;
        }

        notifyIcon.Text = UiTextService.Instance["App.Name"];
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        var previousMenu = notifyIcon.ContextMenuStrip;
        notifyIcon.ContextMenuStrip = BuildMenu();
        previousMenu?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var ui = UiTextService.Instance;
        var menu = new ContextMenuStrip();
        menu.Items.Add(ui["Tray.CaptureRegion"], null, (_, _) => captureRegionAction());
        menu.Items.Add(ui["Tray.CaptureActiveWindow"], null, (_, _) => captureActiveWindowAction());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ui["Tray.OpenControlCenter"], null, (_, _) => openAction());
        menu.Items.Add(ui["Tray.OpenImage"], null, (_, _) => openImageAction());
        menu.Items.Add(ui["Tray.Settings"], null, (_, _) => settingsAction());
        menu.Items.Add(ui["Tray.About"], null, (_, _) => aboutAction());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ui["Tray.Exit"], null, (_, _) => exitAction());
        return menu;
    }
}
