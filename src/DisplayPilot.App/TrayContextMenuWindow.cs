// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Windows.Shell;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace DisplayPilot.App;

internal sealed class TrayContextMenuWindow : Window
{
    private const int MenuWidth = 180;
    private const int MenuHeight = 100;
    private bool _shown;

    public TrayContextMenuWindow()
    {
        Title = "DisplayPilot";

        var advancedButton = CreateMenuButton("Advanced");
        advancedButton.Click += (_, _) =>
        {
            AdvancedInvoked?.Invoke(this, EventArgs.Empty);
            Close();
        };

        var exitButton = CreateMenuButton("Exit");
        exitButton.Click += (_, _) =>
        {
            ExitInvoked?.Invoke(this, EventArgs.Empty);
            Close();
        };

        var buttons = new StackPanel
        {
            Spacing = 2,
        };
        buttons.Children.Add(advancedButton);
        buttons.Children.Add(exitButton);

        Content = new Border
        {
            Padding = new Thickness(6),
            Background = Application.Current.Resources[
                "ApplicationPageBackgroundThemeBrush"] as Brush,
            BorderBrush = Application.Current.Resources[
                "CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = buttons,
        };

        AppWindow.SetPresenter(OverlappedPresenter.CreateForContextMenu());
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        AppWindow.IsShownInSwitchers = false;
        Activated += TrayContextMenuWindow_Activated;
    }

    public event EventHandler? AdvancedInvoked;

    public event EventHandler? ExitInvoked;

    public void ShowAt(NotificationAreaBounds? anchor)
    {
        var window = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = WindowWorkArea.GetScale(window);
        var width = (int)Math.Round(MenuWidth * scale);
        var height = (int)Math.Round(MenuHeight * scale);
        AppWindow.Resize(new SizeInt32(width, height));

        if (anchor is { } bounds &&
            WindowWorkArea.TryGetNearest(bounds, out var workArea))
        {
            var placement = FlyoutPlacement.Calculate(
                bounds,
                workArea,
                width,
                height);
            AppWindow.Move(new PointInt32(placement.Left, placement.Top));
        }

        _shown = true;
        AppWindow.Show();
        Activate();
    }

    private static Button CreateMenuButton(string text)
    {
        return new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
    }

    private void TrayContextMenuWindow_Activated(
        object sender,
        WindowActivatedEventArgs args)
    {
        if (_shown &&
            args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Close();
        }
    }
}
