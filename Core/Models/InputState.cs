namespace BrokenithmWindows.Core.Models;

public readonly record struct InputState(
    uint Slider,
    int AirHeight = 6,
    bool Test = false,
    bool Service = false
)
{
    public static InputState Empty => new(0, 6);
}

public readonly record struct TouchPoint(uint Id, double X, double Y, double Size);

public sealed record InputOptions(
    bool EnableAir = true,
    bool SimpleAir = false,
    bool UseContactSize = false,
    double FatThreshold = .027,
    double ExtraFatThreshold = .035
);
