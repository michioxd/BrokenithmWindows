using BrokenithmWindows.Core.Models;

namespace BrokenithmWindows.Core.Input;

public static class TouchProcessor
{
    public static InputState Process(IReadOnlyList<TouchPoint> contacts, InputOptions options)
    {
        uint sensors = 0;
        int air = 6;
        for (int contact = 0; contact < contacts.Count; contact++)
        {
            var point = contacts[contact];
            if (point.X < 0 || point.X > 1 || point.Y < 0 || point.Y > 1)
                continue;
            if (options.EnableAir && point.Y <= .375)
            {
                int height = point.Y <= .1875 ? 0 : Math.Min(6, (int)((point.Y - .1875) / .03125));
                air = Math.Min(air, options.SimpleAir ? 0 : height);
                continue;
            }
            SliderProcessor.AddContact(ref sensors, point.X, point.Size, options);
        }
        return new(sensors, air);
    }
}
