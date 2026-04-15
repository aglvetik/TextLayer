using System.Windows;
using TextLayer.App.Models;
using TextLayer.App.Views;

namespace TextLayer.App.Services;

public sealed class RegionSelectionService(ScreenGeometryService screenGeometryService)
{
    public async Task<ScreenSelectionResult?> SelectRegionAsync(CancellationToken cancellationToken)
    {
        var completionSource = new TaskCompletionSource<ScreenSelectionResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var windows = screenGeometryService.GetAllMonitors()
            .Select(monitor => CreateSelectionWindow(monitor, completionSource))
            .ToArray();

        try
        {
            foreach (var window in windows)
            {
                window.Show();
            }

            windows[^1].Activate();

            using var registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
            var result = await completionSource.Task.ConfigureAwait(true);

            foreach (var window in windows)
            {
                window.Close();
            }

            // Give the transparent picker windows a moment to disappear before capturing the screen.
            await Task.Delay(70, cancellationToken).ConfigureAwait(true);
            return result;
        }
        catch (OperationCanceledException)
        {
            foreach (var window in windows)
            {
                window.Close();
            }

            return null;
        }
    }

    private static RegionSelectionWindow CreateSelectionWindow(MonitorInfo monitor, TaskCompletionSource<ScreenSelectionResult?> completionSource)
    {
        var window = new RegionSelectionWindow(monitor);
        window.SelectionCompleted += (_, result) => completionSource.TrySetResult(result);
        window.Cancelled += (_, _) => completionSource.TrySetResult(null);
        return window;
    }
}
