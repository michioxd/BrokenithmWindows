using BrokenithmWindows.Core.Models;

namespace BrokenithmWindows.Core.Input;

public static class SliderProcessor
{
    public static void AddContact(ref uint sensors, double x, double size, InputOptions options)
    {
        double position = x * 16;
        int column = Math.Min(15, (int)position);
        double quarter = (position - column) * 4;
        if (!options.UseContactSize)
        {
            Add(ref sensors, column);
            if (column > 0)
            {
                if (quarter < 1)
                    Add(ref sensors, column - 1);
            }
            else if (quarter > 3)
                Add(ref sensors, column + 1);
            return;
        }
        uint before = sensors;
        AddCandidate(ref sensors, before, column);
        int left = Math.Max(0, column - 1),
            left2 = Math.Max(0, column - 2);
        if (quarter <= 1)
        {
            AddCandidate(ref sensors, before, left);
            if (size >= options.ExtraFatThreshold)
            {
                AddCandidate(ref sensors, before, left2);
                AddCandidate(ref sensors, before, column + 1);
            }
        }
        else if (quarter <= 3)
        {
            if (size >= options.FatThreshold)
            {
                AddCandidate(ref sensors, before, left);
                AddCandidate(ref sensors, before, column + 1);
            }
            if (size >= options.ExtraFatThreshold)
            {
                AddCandidate(ref sensors, before, left2);
                AddCandidate(ref sensors, before, column + 2);
            }
        }
        else
        {
            AddCandidate(ref sensors, before, column + 1);
            if (size >= options.ExtraFatThreshold)
            {
                AddCandidate(ref sensors, before, left);
                AddCandidate(ref sensors, before, column + 2);
            }
        }
    }

    private static void Add(ref uint sensors, int column) =>
        AddCandidate(ref sensors, sensors, column);

    private static void AddCandidate(ref uint sensors, uint before, int column)
    {
        if (column is < 0 or > 15)
            return;
        int index = column * 2;
        if ((before & (1u << index)) != 0)
            index++;
        sensors |= 1u << index;
    }
}
