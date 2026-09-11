using System.Windows.Input;

namespace BrokenithmWindows.ViewModels;

public sealed class AsyncCommand(Func<Task> execute, Action<Exception> failed) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_running;

    public async void Execute(object? parameter)
    {
        if (_running)
            return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await execute();
        }
        catch (Exception ex)
        {
            failed(ex);
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
