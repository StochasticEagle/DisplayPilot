// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace DisplayPilot.App;

public sealed partial class MainWindow : Window
{
    private readonly IMonitorDiscoveryService _monitorDiscovery = new DisplayConfigMonitorDiscovery();
    private readonly DdcBrightnessProbeService _ddcProbeService = new();
    private IReadOnlyList<MonitorDisplayInfo> _activeMonitors = [];
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

    private async void ProbeDdcButton_Click(object sender, RoutedEventArgs e)
    {
        await ProbeDdcBrightnessAsync();
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
        };

        data.SetText(_diagnosticReport);
        try
        {
            Clipboard.SetContent(data);
            CopyReportButton.Content = "Copied";
        }
        catch (COMException exception)
        {
            ReportClipboardFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportClipboardFailure(exception);
        }
    }

    private async Task RefreshDisplaysAsync()
    {
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Scanning active Windows display paths...";
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            var monitors = await Task.Run(_monitorDiscovery.DiscoverActiveMonitors);

            _activeMonitors = monitors;
            MonitorList.ItemsSource = monitors.Select(MonitorCardViewModel.NotProbed).ToArray();
            EmptyState.Visibility = monitors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Found {0} active display {1} at {2:T}.",
                monitors.Count,
                monitors.Count == 1 ? "path" : "paths",
                DateTimeOffset.Now);
            _diagnosticReport = BuildDiagnosticReport(monitors, ddcProbes: null, error: null);
        }
        catch (Win32Exception exception)
        {
            MonitorList.ItemsSource = null;
            _activeMonitors = [];
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = $"Display discovery failed: {exception.Message}";
            _diagnosticReport = BuildDiagnosticReport([], ddcProbes: null, error: exception);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task ProbeDdcBrightnessAsync()
    {
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Reading brightness VCP 0x10 from physical monitors...";

        try
        {
            var probes = await Task.Run(() => _ddcProbeService.ProbeBrightness(_activeMonitors));
            MonitorList.ItemsSource = probes.Select(MonitorCardViewModel.FromProbe).ToArray();

            var readableCount = probes.Sum(probe => probe.PhysicalMonitors.Count(
                result => result.Status == DdcBrightnessProbeStatus.ReadSucceeded));
            StatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Read brightness from {0} physical {1} at {2:T}; no settings were changed.",
                readableCount,
                readableCount == 1 ? "monitor" : "monitors",
                DateTimeOffset.Now);
            _diagnosticReport = BuildDiagnosticReport(_activeMonitors, probes, error: null);
        }
        catch (Win32Exception exception)
        {
            StatusText.Text = $"DDC/CI probe failed: {exception.Message}";
            _diagnosticReport = BuildDiagnosticReport(_activeMonitors, ddcProbes: null, error: exception);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            CopyReportButton.IsEnabled = true;
        }
    }

    private static string BuildDiagnosticReport(
        IReadOnlyList<MonitorDisplayInfo> monitors,
        IReadOnlyList<MonitorDdcProbeInfo>? ddcProbes,
        Win32Exception? error)
    {
        var report = new StringBuilder();
        report.AppendLine("DisplayPilot active display-path report");
        report.Append("Captured: ").AppendLine(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        report.Append("OS: ").AppendLine(RuntimeInformation.OSDescription);
        report.Append("Process architecture: ").AppendLine(RuntimeInformation.ProcessArchitecture.ToString());
        report.Append("Display paths: ").AppendLine(monitors.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("Privacy: device paths can identify a local display instance; review before sharing");
        report.AppendLine(ddcProbes is null
            ? "DDC/CI commands issued: no"
            : "DDC/CI commands issued: read-only brightness VCP 0x10 queries; no writes");

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

            var probe = ddcProbes?.FirstOrDefault(candidate =>
                string.Equals(candidate.Display.DevicePath, monitor.DevicePath, StringComparison.OrdinalIgnoreCase));
            if (probe is null)
            {
                report.AppendLine("DDC/CI: not probed");
                continue;
            }

            foreach (var physicalMonitor in probe.PhysicalMonitors)
            {
                report.Append("DDC/CI status: ").AppendLine(physicalMonitor.Status.ToString());
                report.Append("Physical description: ").AppendLine(physicalMonitor.PhysicalMonitorDescription);
                report.Append("Brightness current: ").AppendLine(physicalMonitor.CurrentValue.ToString(CultureInfo.InvariantCulture));
                report.Append("Brightness maximum: ").AppendLine(physicalMonitor.MaximumValue.ToString(CultureInfo.InvariantCulture));
                report.Append("Read attempts: ").AppendLine(physicalMonitor.AttemptCount.ToString(CultureInfo.InvariantCulture));
                report.Append("Win32 error: ").AppendLine(physicalMonitor.Win32Error.ToString(CultureInfo.InvariantCulture));
            }
        }

        return report.ToString();
    }

    private void ReportClipboardFailure(Exception exception)
    {
        CopyReportButton.Content = "Copy failed — retry";
        StatusText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Could not open the Windows clipboard (0x{0:X8}). The report remains available; retry copying.",
            exception.HResult);
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
