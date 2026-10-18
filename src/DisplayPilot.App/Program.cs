// SPDX-License-Identifier: Unlicense
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace DisplayPilot.App;

public static partial class Program
{
    private const uint Infinite = 0xFFFFFFFF;
    private static readonly string InstanceKey = CreateInstanceKey();

    internal static event EventHandler<AppActivationArguments>? ActivationRedirected;

    // Called directly by the small native Release launcher; no application JSON files.
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int NativeMain(nint ignored, int reserved)
    {
        _ = ignored;
        _ = reserved;
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Contains("--verify-managed-host", StringComparer.OrdinalIgnoreCase))
        {
            // The CI smoke test must resolve framework and local WinUI dependencies
            // without creating a window or touching per-user startup registration.
            _ = typeof(Microsoft.UI.Xaml.Application).Assembly.GetName();
            _ = typeof(MainWindow).Assembly.GetName();
            return 0;
        }

        return Main(arguments);
    }

    [STAThread]
    public static int Main(string[] args)
    {
        _ = args;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (RedirectToExistingInstance())
        {
            return 0;
        }

        Application.Start(initializationParameters =>
        {
            _ = initializationParameters;
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    private static bool RedirectToExistingInstance()
    {
        var activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (instance.IsCurrent)
        {
            instance.Activated += (_, redirectedArguments) =>
                ActivationRedirected?.Invoke(null, redirectedArguments);
            return false;
        }

        RedirectActivation(activationArguments, instance);
        return true;
    }

    private static void RedirectActivation(
        AppActivationArguments activationArguments,
        AppInstance instance)
    {
        var completedEvent = CreateEvent(0, true, false, null);
        if (completedEvent == 0)
        {
            throw new InvalidOperationException(
                $"Could not create the activation event (Win32 error {Marshal.GetLastPInvokeError()}).");
        }

        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await instance.RedirectActivationToAsync(activationArguments);
                }
                finally
                {
                    _ = SetEvent(completedEvent);
                }
            });

            WaitForRedirect(completedEvent);
        }
        finally
        {
            _ = CloseHandle(completedEvent);
        }
    }

    private static unsafe void WaitForRedirect(nint completedEvent)
    {
        nint* handles = stackalloc nint[1];
        handles[0] = completedEvent;
        _ = CoWaitForMultipleObjects(
            0,
            Infinite,
            1,
            handles,
            out _);
    }

    private static string CreateInstanceKey()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return $"DisplayPilot-{identity.User?.Value ?? Environment.UserName}";
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateEvent(
        nint eventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState,
        string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetEvent(nint eventHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("ole32.dll")]
    private static unsafe partial uint CoWaitForMultipleObjects(
        uint flags,
        uint timeoutMilliseconds,
        ulong handleCount,
        nint* handles,
        out uint index);
}
