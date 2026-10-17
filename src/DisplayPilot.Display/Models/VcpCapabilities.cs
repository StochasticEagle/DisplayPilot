// DisplayPilot MCCS capability data model.
// Numeric feature codes and protocol fields are specified by VESA MCCS.
namespace DisplayPilot.Display.Models;

public sealed class VcpCapabilities
{
    public string Raw { get; set; } = "";
    public string? Model { get; set; }
    public string? Type { get; set; }
    public string? Protocol { get; set; }
    public string? MccsVersion { get; set; }
    public List<byte> SupportedCommands { get; set; } = new();
    public Dictionary<byte, VcpCodeInfo> SupportedVcpCodes { get; set; } = new();
    public List<WindowCapability> Windows { get; set; } = new();

    public bool HasWindowSupport => Windows.Count > 0;
    public bool SupportsVcpCode(byte code) => SupportedVcpCodes.ContainsKey(code);
    public VcpCodeInfo? GetVcpCodeInfo(byte code)
        => SupportedVcpCodes.TryGetValue(code, out var item) ? item : null;
    public bool HasDiscreteValues(byte code) => GetVcpCodeInfo(code)?.HasDiscreteValues == true;
    public IReadOnlyList<int>? GetSupportedValues(byte code) => GetVcpCodeInfo(code)?.SupportedValues;
    public List<string> GetVcpCodesAsHexStrings()
        => SupportedVcpCodes.Keys.OrderBy(code => code).Select(code => $"0x{code:X2}").ToList();
    public IEnumerable<VcpCodeInfo> GetSortedVcpCodes()
        => SupportedVcpCodes.OrderBy(pair => pair.Key).Select(pair => pair.Value);
    public static VcpCapabilities Empty => new();
    public override string ToString() => $"Model: {Model}, VCP Codes: {SupportedVcpCodes.Count}";
}

public readonly struct VcpCodeInfo
{
    public byte Code { get; }
    public string Name { get; }
    public IReadOnlyList<int> SupportedValues { get; }
    public bool HasDiscreteValues => SupportedValues?.Count > 0;
    public bool IsContinuous => !HasDiscreteValues;
    public string FormattedCode => $"0x{Code:X2}";
    public string FormattedTitle => $"{Name} ({FormattedCode})";

    public VcpCodeInfo(byte code, string name, IReadOnlyList<int>? supportedValues = null)
    {
        Code = code;
        Name = name;
        SupportedValues = supportedValues ?? Array.Empty<int>();
    }

    public override string ToString()
        => HasDiscreteValues
            ? $"{FormattedTitle}: {string.Join(", ", SupportedValues)}"
            : $"{FormattedTitle}: Continuous";
}

public readonly record struct WindowSize(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";
}

public readonly record struct WindowArea(int X1, int Y1, int X2, int Y2)
{
    public int Width => X2 - X1;
    public int Height => Y2 - Y1;
    public override string ToString() => $"({X1},{Y1})-({X2},{Y2})";
}

public readonly record struct WindowCapability(
    int WindowNumber,
    string Type,
    WindowArea Area,
    WindowSize MaxSize,
    WindowSize MinSize,
    int WindowId)
{
    public override string ToString() => $"Window{WindowNumber}: {Type}, {Area}";
}
