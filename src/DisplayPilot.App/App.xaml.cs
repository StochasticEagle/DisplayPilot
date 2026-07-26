// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using Microsoft.UI.Xaml;

namespace DisplayPilot.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        _window = window;
        var notificationAreaReady = window.InitializeNotificationArea();
        await window.InitializeAsync();
        if (!notificationAreaReady)
        {
            window.Activate();
        }
    }
}
