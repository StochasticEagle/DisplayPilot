using System.Runtime.InteropServices;
namespace DisplayPilot.Windows.Theme;

public sealed partial class WindowsThemeNotifier : IThemeNotifier
{
    private const nint AllWindows = 0xffff;
    private const uint AbortHungWindows = 2;
    private const uint MessageTimeout = 5000;

    public void NotifyThemeChanged()
    {
        _ = BroadcastText(AllWindows, 0x001A, 0, "ImmersiveColorSet",
            AbortHungWindows, MessageTimeout, out _);
        Broadcast(0x031A);
    }

    public void NotifyColorPrevalenceChanged()
    {
        NotifyThemeChanged();
        Broadcast(0x0320);
    }

    private static void Broadcast(uint message)
        => _ = BroadcastNumber(AllWindows, message, 0, 0,
            AbortHungWindows, MessageTimeout, out _);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint BroadcastText(nint window, uint message, nuint wparam,
        string text, uint flags, uint timeout, out nuint result);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial nint BroadcastNumber(nint window, uint message, nuint wparam,
        nint parameter, uint flags, uint timeout, out nuint result);
}
