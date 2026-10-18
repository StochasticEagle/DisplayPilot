// SPDX-License-Identifier: MIT
// Independent section scanner for VESA MCCS capability strings.
using System.Globalization;
using DisplayPilot.Display.Models;
namespace DisplayPilot.Display.Mccs;

public static class MccsCapabilitiesParser
{
    public static MccsParseResult Parse(string? text)
    {
        var caps = new MccsDisplayProfile { Raw = text ?? "" };
        if (string.IsNullOrWhiteSpace(text)) return new(caps, Array.Empty<ParseError>());
        foreach (var (name, value) in Sections(text))
        {
            switch (name.ToLowerInvariant())
            {
                case "prot": caps.Protocol = value.Trim(); break;
                case "type": caps.Type = value.Trim(); break;
                case "model": caps.Model = value.Trim(); break;
                case "mccs_ver": caps.MccsVersion = value.Trim(); break;
                case "cmds": caps.SupportedCommands = Hex(value).ToList(); break;
                case "vcp":
                    foreach (var (code, detail) in Entries(value))
                        caps.Features[code] = new(code, VcpNames.GetCodeName(code), Hex(detail).Select(x => (int)x).ToArray());
                    break;
                case "vcpname":
                    foreach (var (code, detail) in Entries(value))
                        caps.Features[code] = new(code, detail.Trim(), caps.FeatureValues(code));
                    break;
                default:
                    if (name.StartsWith("window", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(name.AsSpan(6), out int number))
                        caps.Windows.Add(Window(number, value));
                    break;
            }
        }
        return new(caps, Array.Empty<ParseError>());
    }

    private static IEnumerable<(string, string)> Sections(string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            while (pos < text.Length && !char.IsLetter(text[pos])) pos++;
            int start = pos;
            while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_')) pos++;
            if (start == pos) yield break;
            string name = text[start..pos];
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos == text.Length || text[pos] != '(') continue;
            int first = ++pos, depth = 1;
            while (pos < text.Length && depth != 0)
            {
                depth += text[pos] == '(' ? 1 : text[pos] == ')' ? -1 : 0;
                if (depth != 0) pos++;
            }
            yield return (name, text[first..pos]);
            if (pos < text.Length) pos++;
        }
    }

    private static IEnumerable<byte> Hex(string text)
    {
        for (int pos = 0; pos < text.Length;)
        {
            if (pos + 1 < text.Length && Uri.IsHexDigit(text[pos]) && Uri.IsHexDigit(text[pos + 1]))
            {
                yield return byte.Parse(text.AsSpan(pos, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                pos += 2;
            }
            else pos++;
        }
    }

    private static IEnumerable<(byte Code, string Detail)> Entries(string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos + 1 >= text.Length) yield break;
            if (!Uri.IsHexDigit(text[pos]) || !Uri.IsHexDigit(text[pos + 1])) { pos++; continue; }
            byte code = byte.Parse(text.AsSpan(pos, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            pos += 2;
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            string detail = "";
            if (pos < text.Length && text[pos] == '(')
            {
                int first = ++pos, depth = 1;
                while (pos < text.Length && depth != 0)
                {
                    depth += text[pos] == '(' ? 1 : text[pos] == ')' ? -1 : 0;
                    if (depth != 0) pos++;
                }
                detail = text[first..pos];
                if (pos < text.Length) pos++;
            }
            yield return (code, detail);
        }
    }

    private static MccsWindow Window(int index, string raw)
    {
        string type = "";
        int id = 0;
        PixelBounds area = default;
        PixelSize max = default, min = default;
        foreach (var (name, value) in Sections(raw))
        {
            var n = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out int x) ? x : 0).ToArray();
            switch (name.ToLowerInvariant())
            {
                case "type": type = value.Trim(); break;
                case "area" when n.Length >= 4: area = new(n[0], n[1], n[2], n[3]); break;
                case "max" when n.Length >= 2: max = new(n[0], n[1]); break;
                case "min" when n.Length >= 2: min = new(n[0], n[1]); break;
                case "window" when n.Length > 0: id = n[0]; break;
            }
        }
        return new(index, type, area, max, min, id);
    }
}

public readonly record struct ParseError(int Position, string Message)
{
    public override string ToString() => $"[{Position}] {Message}";
}

public sealed class MccsParseResult(MccsDisplayProfile capabilities, IReadOnlyList<ParseError> errors)
{
    public MccsDisplayProfile Capabilities { get; } = capabilities;
    public IReadOnlyList<ParseError> Errors { get; } = errors;
    public bool HasErrors => Errors.Count > 0;
    public bool IsValid => !HasErrors && Capabilities.Features.Count > 0;
}
