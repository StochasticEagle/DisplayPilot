// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DisplayPilot.Display.Discovery;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace DisplayPilot.App;

public sealed partial class MainWindow : Window
{
    private readonly IMonitorDiscoveryService _monitorDiscovery = new DisplayConfigMonitorDiscovery();
    private bool _initialScanStarted;
    private string _diagnosticReport = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        SystemText.Text = GetSystemSummary();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialScanStarted)
        {
            return;
        }

        _initialScanStarted = true;
        await RefreshDisplaysAsync();
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDisplaysAsync();
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
        };

        data.SetText(_diagnosticReport);
        Clipboard.SetContent(data);
        Clipboard.Flush();
        CopyReportButton.Content = "Copied";
    }

    private async Task RefreshDisplaysAsync()
    {
        RescanButton.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Scanning active Windows display paths...";
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            var monitors = await Task.Run(_monitorDiscovery.DiscoverActiveMonitors);

            MonitorList.ItemsSource = monitors;
            EmptyState.Visibility = monitors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Found {0} active display {1} at {2:T}.",
                monitors.Count,
                monitors.Count == 1 ? "path" : "paths",
                DateTimeOffset.Now);
            _diagnosticReport = BuildDiagnosticReport(monitors, error: null);
        }
        catch (Win32Exception exception)
        {
            MonitorList.ItemsSource = null;
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = $"Display discovery failed: {exception.Message}";
            _diagnosticReport = BuildDiagnosticReport([], exception);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }
    }

    private static string BuildDiagnosticReport(
        IReadOnlyList<MonitorDisplayInfo> monitors,
        Win32Exception? error)
    {
        var report = new StringBuilder();
        report.AppendLine("DisplayPilot active display-path report");
        report.Append("Captured: ").AppendLine(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        report.Append("OS: ").AppendLine(RuntimeInformation.OSDescription);
        report.Append("Process architecture: ").AppendLine(RuntimeInformation.ProcessArchitecture.ToString());
        report.Append("Display paths: ").AppendLine(monitors.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("DDC/CI commands issued: no");

        if (error is not null)
        {
            report.Append("Win32 error: ").Append(error.NativeErrorCode).Append(" - ").AppendLine(error.Message);
        }

        for (var index = 0; index < monitors.Count; index++)
        {
            var monitor = monitors[index];
            report.AppendLine();
            report.Append("Display ").Append(index + 1).Append(": ").AppendLine(monitor.FriendlyName);
            report.Append("Windows name: ").AppendLine(monitor.GdiDeviceName);
            report.Append("Monitor number: ").AppendLine(monitor.MonitorNumber.ToString(CultureInfo.InvariantCulture));
            report.Append("Device path: ").AppendLine(monitor.DevicePath);
        }

        return report.ToString();
    }

    private static string GetSystemSummary()
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            "{0} · {1}",
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture);
    }
}
