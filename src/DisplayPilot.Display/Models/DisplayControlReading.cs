namespace DisplayPilot.Display.Models;

public readonly record struct DisplayControlReading
{
    public int Current { get; }
    public int Minimum { get; }
    public int Maximum { get; }
    public DateTime Timestamp { get; }

    public DisplayControlReading(int current, int minimum, int maximum)
    {
        Current = current;
        Minimum = minimum;
        Maximum = maximum;
        Timestamp = DateTime.Now;
    }

    public DisplayControlReading(int current, int maximum) : this(current, 0, maximum) { }

    public bool IsValid => Maximum > Minimum && Current >= Minimum && Current <= Maximum;
    public static DisplayControlReading Invalid => new(-1, -1, -1);

    public int ToPercentage() => IsValid
        ? (int)Math.Round((Current - (double)Minimum) * 100 / (Maximum - Minimum))
        : 0;

    public static int FromPercentage(int percent, int maximum, int minimum = 0)
        => maximum <= minimum ? minimum
            : minimum + (int)Math.Round(Math.Clamp(percent, 0, 100)
                * (double)(maximum - minimum) / 100);

    public override string ToString() =>
        IsValid ? $"{Current}/{Maximum} ({ToPercentage()}%)" : "Invalid";
}
