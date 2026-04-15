using System.ComponentModel;
using System.IO;
using System.Windows;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using IDataObject = System.Windows.IDataObject;
using TextLayer.App.ViewModels;
using TextLayer.Application.Models;
using TextLayer.App.Views;

namespace TextLayer.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private bool allowExit;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;

        Viewer.SelectionChanged += (_, selection) => this.viewModel.UpdateSelection(selection);
        Viewer.HoverWordChanged += (_, word) => this.viewModel.UpdateHoverWord(word?.Text);
        Viewer.ZoomChanged += (_, zoom) => this.viewModel.UpdateZoom(zoom);
        Viewer.CopyRequested += (_, _) =>
        {
            if (this.viewModel.CopySelectionCommand.CanExecute(null))
            {
                this.viewModel.CopySelectionCommand.Execute(null);
            }
        };

        PreviewDragOver += MainWindow_OnPreviewDragOver;
        PreviewDrop += MainWindow_OnPreviewDrop;
    }

    public async Task InitializeAsync()
    {
        await viewModel.InitializeAsync();
        ApplyWindowPlacement(viewModel.CurrentSettings.WindowPlacement);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!allowExit)
        {
            if (viewModel.CurrentSettings.CloseToTrayOnClose)
            {
                _ = viewModel.PersistWindowPlacementAsync(CaptureWindowPlacement());
                Hide();
                e.Cancel = true;
                return;
            }

            e.Cancel = true;
            _ = ((App)System.Windows.Application.Current).RequestFullShutdownAsync();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (!allowExit)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void FitToWindowButton_OnClick(object sender, RoutedEventArgs e) => Viewer.FitToWindow();

    private void ActualSizeButton_OnClick(object sender, RoutedEventArgs e) => Viewer.ActualSize();

    private void ZoomInButton_OnClick(object sender, RoutedEventArgs e) => Viewer.ZoomIn();

    private void ZoomOutButton_OnClick(object sender, RoutedEventArgs e) => Viewer.ZoomOut();

    private void ResetViewButton_OnClick(object sender, RoutedEventArgs e) => Viewer.ResetView();

    private async void SettingsButton_OnClick(object sender, RoutedEventArgs e) => await ShowSettingsDialogAsync();

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ((App)System.Windows.Application.Current).RequestFullShutdownAsync();
    }

    private void MainWindow_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFile(e.Data) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void MainWindow_OnPreviewDrop(object sender, DragEventArgs e)
    {
        var droppedFile = TryGetDroppedFile(e.Data);
        if (droppedFile is not null)
        {
            await viewModel.OpenImageFromPathAsync(droppedFile);
        }

        e.Handled = true;
    }

    private static string? TryGetDroppedFile(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = (string[]?)dataObject.GetData(DataFormats.FileDrop);
        return files?.FirstOrDefault(file => File.Exists(file));
    }

    private WindowPlacementSettings CaptureWindowPlacement()
    {
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        return new WindowPlacementSettings(
            Left: bounds.Left,
            Top: bounds.Top,
            Width: bounds.Width,
            Height: bounds.Height,
            IsMaximized: WindowState == WindowState.Maximized);
    }

    private void ApplyWindowPlacement(WindowPlacementSettings placement)
    {
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;

        if (placement.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    public void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void OpenImageUsingDialog()
    {
        RestoreFromTray();
        if (viewModel.OpenImageCommand.CanExecute(null))
        {
            viewModel.OpenImageCommand.Execute(null);
        }
    }

    public void ShowSettingsDialog() => _ = ShowSettingsDialogAsync();

    public async Task ShowSettingsDialogAsync()
    {
        RestoreFromTray();
        var settingsViewModel = new SettingsWindowViewModel(viewModel.SnapshotSettingsWithWindowPlacement(CaptureWindowPlacement()));
        var settingsWindow = new SettingsWindow(settingsViewModel)
        {
            Owner = this,
        };

        if (settingsWindow.ShowDialog() == true)
        {
            await viewModel.ApplySettingsAsync(settingsViewModel.ToSettings(CaptureWindowPlacement()));
        }
    }

    public void ShowAboutDialog()
    {
        RestoreFromTray();
        var aboutWindow = new AboutWindow
        {
            Owner = this,
        };

        aboutWindow.ShowDialog();
    }

    public Task PersistStateAsync()
        => viewModel.PersistWindowPlacementAsync(CaptureWindowPlacement());

    public void PrepareForExit() => allowExit = true;
}
