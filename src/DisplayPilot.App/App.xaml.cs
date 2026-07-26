// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace DisplayPilot.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Program.ActivationRedirected += Program_ActivationRedirected;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var startupLaunch = IsStartupLaunch(args.Arguments);
        var window = new MainWindow();
        _window = window;
        var notificationAreaReady = window.InitializeNotificationArea();
        await window.InitializeAsync(includeDisplays: !startupLaunch);
        if (!notificationAreaReady)
        {
            window.Activate();
        }
    }

    private void Program_ActivationRedirected(
        object? sender,
        AppActivationArguments arguments)
    {
        if (IsStartupActivation(arguments) || _window is not MainWindow window)
        {
            return;
        }

        _ = window.DispatcherQueue.TryEnqueue(window.ShowFromExternalActivation);
    }

    private static bool IsStartupActivation(AppActivationArguments arguments)
    {
        return arguments.Data is ILaunchActivatedEventArgs launchArguments &&
            IsStartupLaunch(launchArguments.Arguments);
    }

    private static bool IsStartupLaunch(string? arguments)
    {
        return arguments?.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("--startup", StringComparer.OrdinalIgnoreCase) == true;
    }
}
