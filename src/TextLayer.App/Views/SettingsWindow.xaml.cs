using System.Windows;
using TextLayer.App.ViewModels;

namespace TextLayer.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
