using System.Windows;
using TextLayer.App.Services;

namespace TextLayer.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        VersionTextBlock.Text = UiTextService.Instance.Format("About.Version", version);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
