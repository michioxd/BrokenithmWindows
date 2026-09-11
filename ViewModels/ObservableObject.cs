using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BrokenithmWindows.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new(name));

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Notify(name);
    }
}
