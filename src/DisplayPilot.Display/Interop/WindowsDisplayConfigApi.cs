using System.ComponentModel;
using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Interop;

public sealed class WindowsDisplayConfigApi : IDisplayConfigApi
{
    private const int InsufficientBuffer = 122;

    public unsafe IReadOnlyList<DisplayConfigPathDescriptor> QueryActiveDisplayPaths()
    {
        // Topology may change between sizing and reading. Retry only on that race.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int status = DisplayNative.GetDisplayConfigBufferSizes(
                DisplayInteropConstants.QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
            if (status != 0) throw new Win32Exception(status);

            var paths = new ActivePathRecord[pathCount];
            var modes = new ModeRecord[modeCount];
            fixed (ActivePathRecord* pathData = paths)
            fixed (ModeRecord* modeData = modes)
                status = DisplayNative.QueryDisplayConfig(
                    DisplayInteropConstants.QdcOnlyActivePaths, ref pathCount, pathData,
                    ref modeCount, modeData, 0);

            if (status == InsufficientBuffer) continue;
            if (status != 0) throw new Win32Exception(status);

            var displays = new List<DisplayConfigPathDescriptor>((int)pathCount);
            for (int i = 0; i < pathCount; i++)
            {
                ref readonly var path = ref paths[i];
                var source = new SourceDisplayName
                {
                    Header = new DisplayNameHeader
                    {
                        Query = DisplayInteropConstants.DisplayconfigDeviceInfoGetSourceName,
                        Size = (uint)sizeof(SourceDisplayName),
                        Adapter = path.SourceAdapter,
                        Id = path.SourceId
                    }
                };
                if (DisplayNative.ReadDisplayName(&source) != 0) continue;

                var target = new TargetDisplayName
                {
                    Header = new DisplayNameHeader
                    {
                        Query = DisplayInteropConstants.DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)sizeof(TargetDisplayName),
                        Adapter = path.TargetAdapter,
                        Id = path.TargetId
                    }
                };
                if (DisplayNative.ReadDisplayName(&target) != 0) continue;

                displays.Add(new DisplayConfigPathDescriptor(
                    target.GetPath(), source.GetName(), target.GetFriendlyName(), i + 1));
            }
            return displays;
        }

        throw new Win32Exception(InsufficientBuffer, "Display topology changed during enumeration.");
    }
}
