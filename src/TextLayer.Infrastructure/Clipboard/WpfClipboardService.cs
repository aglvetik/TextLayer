using TextLayer.Application.Abstractions;

namespace TextLayer.Infrastructure.Clipboard;

public sealed class WpfClipboardService : IClipboardService
{
    public async Task CopyTextAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var clipboardThread = new Thread(() =>
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(text);
                    completionSource.TrySetResult();
                }
                catch (Exception exception)
                {
                    completionSource.TrySetException(exception);
                }
            });

            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.IsBackground = true;
            clipboardThread.Start();

            using (cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken)))
            {
                await completionSource.Task;
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("TextLayer could not copy text to the clipboard. Please try again.", exception);
        }
    }
}
