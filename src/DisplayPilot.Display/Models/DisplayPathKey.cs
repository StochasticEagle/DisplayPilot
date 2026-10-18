// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Display.Models;

public static class DisplayPathKey
{
    public static string FromDevicePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        int suffix = path.IndexOf("#{", StringComparison.Ordinal);
        return suffix < 0 ? path : path[..suffix];
    }

    public static string FromWmiInstance(string? instance)
    {
        if (string.IsNullOrWhiteSpace(instance)) return string.Empty;
        var parts = instance.Split('\\');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty)) return string.Empty;
        var node = parts[2];
        int suffix = node.LastIndexOf('_');
        if (suffix > 0 && int.TryParse(node.AsSpan(suffix + 1), out _))
            node = node[..suffix];
        return $@"\\?\{parts[0]}#{parts[1]}#{node}";
    }
}
