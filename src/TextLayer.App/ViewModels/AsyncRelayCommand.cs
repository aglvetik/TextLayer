using System.Windows.Input;

namespace TextLayer.App.ViewModels;

public sealed class AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) : ICommand
{
    private readonly Func<Task> executeAsync = executeAsync;
    private readonly Func<bool>? canExecute = canExecute;
    private bool isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !isExecuting && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            isExecuting = true;
            NotifyCanExecuteChanged();
            await executeAsync();
        }
        finally
        {
            isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
