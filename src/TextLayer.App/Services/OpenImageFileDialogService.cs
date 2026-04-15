namespace TextLayer.App.Services;

public sealed class OpenImageFileDialogService : IFileDialogService
{
    public string? OpenImageFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = UiTextService.Instance["FileDialog.Title"],
            Filter = UiTextService.Instance["FileDialog.Filter"],
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
