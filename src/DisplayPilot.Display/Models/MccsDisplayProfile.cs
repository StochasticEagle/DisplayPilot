// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Display.Models;

// MCCS is the VESA protocol; this is DisplayPilot's compact decoded representation.
public sealed class MccsDisplayProfile
{
    public string Raw { get; set; } = "";
    public string? Model { get; set; }
    public string? Type { get; set; }
    public string? Protocol { get; set; }
    public string? MccsVersion { get; set; }
    public List<byte> SupportedCommands { get; set; } = [];
    public Dictionary<byte, MccsVcpFeature> Features { get; } = [];
    public List<MccsWindow> Windows { get; } = [];
    public bool HasWindows => Windows.Count != 0;

    public bool SupportsVcp(byte code) => Features.ContainsKey(code);

    public MccsVcpFeature? FindFeature(byte code) =>
        Features.TryGetValue(code, out var feature) ? feature : null;

    public IReadOnlyList<int>? FeatureValues(byte code) =>
        FindFeature(code)?.SupportedValues;

    public string FormatVcpCodes() =>
        string.Join(", ", Features.Keys.Order().Select(code => $"0x{code:X2}"));
}

public readonly record struct MccsVcpFeature
{
    public byte Code { get; }
    public string Name { get; }
    public IReadOnlyList<int> SupportedValues { get; }
    public bool HasDiscreteValues => SupportedValues.Count != 0;
    public bool IsContinuous => SupportedValues.Count == 0;

    public MccsVcpFeature(byte code, string name, IReadOnlyList<int>? values = null)
    {
        Code = code;
        Name = name;
        SupportedValues = values ?? [];
    }
}

public readonly record struct PixelSize(int Width, int Height);
public readonly record struct PixelBounds(int X1, int Y1, int X2, int Y2)
{
    public int Width => X2 - X1;
    public int Height => Y2 - Y1;
}

public readonly record struct MccsWindow(
    int WindowNumber,
    string Type,
    PixelBounds Area,
    PixelSize MaxSize,
    PixelSize MinSize,
    int WindowId);
