using BrokenithmWindows.Core.Models;

namespace BrokenithmWindows.Core.Input;

public static class SliderProcessor
{
    public static void AddContact(
        ref uint sensors,
        double x,
        double size,
        InputOptions options,
        int? lane = null
    )
    {
        double position = x * 16;
        int column = Math.Min(15, (int)position);
        double quarter = (position - column) * 4;
        if (!options.UseContactSize)
        {
            Add(ref sensors, column, lane);
            if (column > 0)
            {
                if (quarter < 1)
                    Add(ref sensors, column - 1, lane);
            }
            else if (quarter > 3)
                Add(ref sensors, column + 1, lane);
            return;
        }
        uint before = sensors;
        AddCandidate(ref sensors, before, column, lane);
        int left = Math.Max(0, column - 1),
            left2 = Math.Max(0, column - 2);
        if (quarter <= 1)
        {
            AddCandidate(ref sensors, before, left, lane);
            if (size >= options.ExtraFatThreshold)
            {
                AddCandidate(ref sensors, before, left2, lane);
                AddCandidate(ref sensors, before, column + 1, lane);
            }
        }
        else if (quarter <= 3)
        {
            if (size >= options.FatThreshold)
            {
                AddCandidate(ref sensors, before, left, lane);
                AddCandidate(ref sensors, before, column + 1, lane);
            }
            if (size >= options.ExtraFatThreshold)
            {
                AddCandidate(ref sensors, before, left2, lane);
                AddCandidate(ref sensors, before, column + 2, lane);
            }
        }
        else
        {
            AddCandidate(ref sensors, before, column + 1, lane);
            if (size >= options.ExtraFatThreshold)
            {
                AddCandidate(ref sensors, before, left, lane);
                AddCandidate(ref sensors, before, column + 2, lane);
            }
        }
    }

    private static void Add(ref uint sensors, int column, int? lane) =>
        AddCandidate(ref sensors, sensors, column, lane);

    private static void AddCandidate(ref uint sensors, uint before, int column, int? lane)
    {
        if (column is < 0 or > 15)
            return;
        int index = column * 2 + (lane ?? 0);
        if (lane is null && (before & (1u << index)) != 0)
            index++;
        sensors |= 1u << index;
    }
}
