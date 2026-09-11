namespace BrokenithmWindows.Core.Input;

public sealed class AirProcessor
{
    public int Height { get; private set; } = 6;
    private long _lastUpdate;

    public int Update(int target, long milliseconds)
    {
        if (milliseconds - _lastUpdate > 10)
        {
            Height += Math.Sign(Math.Clamp(target, 0, 6) - Height);
            _lastUpdate = milliseconds;
        }
        return Height;
    }
}
