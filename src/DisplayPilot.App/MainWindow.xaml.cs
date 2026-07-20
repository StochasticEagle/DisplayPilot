// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using DisplayPilot.Core.Theme;
using DisplayPilot.Display.Brightness;
using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Wmi;
using DisplayPilot.Windows.Settings;
using DisplayPilot.Windows.Theme;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace DisplayPilot.App;

public sealed partial class MainWindow : Window
{
    private readonly IMonitorDiscoveryService _monitorDiscovery = new DisplayConfigMonitorDiscovery();
    private readonly DdcBrightnessProbeService _ddcProbeService = new();
    private readonly WmiBrightnessProbeService _wmiProbeService = new();
    private readonly BrightnessControlService _brightnessControlService = new();
    private readonly WindowsThemeService _themeService = new();
    private readonly JsonThemeScheduleSettingsStore _themeScheduleSettingsStore = new();
    private IReadOnlyList<MonitorDisplayInfo> _activeMonitors = [];
    private IReadOnlyList<MonitorDdcProbeInfo> _lastDdcProbes = [];
    private IReadOnlyList<WmiBrightnessProbeResult> _lastWmiProbes = [];
    private ThemeState? _themeState;
    private ThemeApplyResult? _lastThemeResult;
    private CustomThemeSchedule? _customThemeSchedule;
    private ThemeScheduleEvaluation? _lastScheduleEvaluation;
    private bool _scheduleWasLoadedFromDisk;
    private string? _scheduleSettingsError;
    private BrightnessWriteResult? _lastBrightnessWriteResult;
    private bool _initialScanStarted;
    private string _diagnosticReport = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        LoadScheduleSettings();
        RefreshSchedulePreview();
        SystemText.Text = GetSystemSummary();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialScanStarted)
        {
            return;
        }

        _initialScanStarted = true;
        RefreshThemeStatus();
        await RefreshDisplaysAsync();
    }

    private void RefreshThemeButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshThemeStatus();
    }

    private async void ApplyLightThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplyThemeAsync(ThemeMode.Light);
    }

    private async void ApplyDarkThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplyThemeAsync(ThemeMode.Dark);
    }

    private void PreviewScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSchedulePreview();
    }

    private void SaveScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        SaveScheduleSettings();
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDisplaysAsync();
    }

    private async void ProbeDdcButton_Click(object sender, RoutedEventArgs e)
    {
        await ProbeDdcBrightnessAsync();
    }

    private async void SetBrightnessButton_Click(object sender, RoutedEventArgs e)
    {
        await SetSelectedBrightnessAsync();
    }

    private void MonitorList_SelectionChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        SetBrightnessButton.IsEnabled = CanSetSelectedDisplay();
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
        SetBrightnessButton.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Scanning active Windows display paths...";
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            var monitors = await Task.Run(_monitorDiscovery.DiscoverActiveMonitors);

            _activeMonitors = monitors;
            _lastDdcProbes = [];
            _lastWmiProbes = [];
            MonitorList.ItemsSource = monitors.Select(MonitorCardViewModel.NotProbed).ToArray();
            EmptyState.Visibility = monitors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Found {0} active display {1} at {2:T}.",
                monitors.Count,
                monitors.Count == 1 ? "path" : "paths",
                DateTimeOffset.Now);
            _diagnosticReport = BuildDiagnosticReport(
                monitors,
                ddcProbes: null,
                wmiProbes: null,
                writeResult: null,
                error: null);
        }
        catch (Win32Exception exception)
        {
            MonitorList.ItemsSource = null;
            _activeMonitors = [];
            _lastDdcProbes = [];
            _lastWmiProbes = [];
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = $"Display discovery failed: {exception.Message}";
            _diagnosticReport = BuildDiagnosticReport(
                [],
                ddcProbes: null,
                wmiProbes: null,
                writeResult: null,
                error: exception);
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
        SetBrightnessButton.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Reading external DDC/CI and internal WMI brightness...";

        try
        {
            var probes = await Task.Run(() => (
                Ddc: _ddcProbeService.ProbeBrightness(_activeMonitors),
                Wmi: _wmiProbeService.ProbeBrightness(_activeMonitors)));
            _lastDdcProbes = probes.Ddc;
            _lastWmiProbes = probes.Wmi;
            UpdateMonitorCards(selectedDevicePath: null);

            var readableCount = _activeMonitors.Count(monitor =>
                probes.Ddc.Any(probe =>
                    string.Equals(probe.Display.DevicePath, monitor.DevicePath, StringComparison.OrdinalIgnoreCase)
                    && probe.PhysicalMonitors.Any(result =>
                        result.Status == DdcBrightnessProbeStatus.ReadSucceeded))
                || probes.Wmi.Any(probe =>
                    string.Equals(probe.Display.DevicePath, monitor.DevicePath, StringComparison.OrdinalIgnoreCase)
                    && probe.Status == WmiBrightnessProbeStatus.ReadSucceeded));
            StatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Read brightness from {0} display {1} at {2:T}; no settings were changed.",
                readableCount,
                readableCount == 1 ? "path" : "paths",
                DateTimeOffset.Now);
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                probes.Ddc,
                probes.Wmi,
                writeResult: null,
                error: null);
        }
        catch (Win32Exception exception)
        {
            StatusText.Text = $"Brightness probe failed: {exception.Message}";
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                ddcProbes: null,
                wmiProbes: null,
                writeResult: null,
                error: exception);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            SetBrightnessButton.IsEnabled = CanSetSelectedDisplay();
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task SetSelectedBrightnessAsync()
    {
        if (MonitorList.SelectedItem is not MonitorCardViewModel selected)
        {
            return;
        }

        var display = _activeMonitors.First(candidate => string.Equals(
            candidate.DevicePath,
            selected.DevicePath,
            StringComparison.OrdinalIgnoreCase));
        var ddcProbe = _lastDdcProbes.First(candidate => string.Equals(
            candidate.Display.DevicePath,
            display.DevicePath,
            StringComparison.OrdinalIgnoreCase));
        var wmiProbe = _lastWmiProbes.First(candidate => string.Equals(
            candidate.Display.DevicePath,
            display.DevicePath,
            StringComparison.OrdinalIgnoreCase));
        var requestedPercent = double.IsNaN(BrightnessValue.Value)
            ? 50
            : Math.Clamp((int)Math.Round(BrightnessValue.Value), 0, 100);

        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        SetBrightnessButton.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        StatusText.Text = $"Setting {display.FriendlyName} brightness to {requestedPercent}%...";
        BrightnessWriteResult? writeResult = null;

        try
        {
            writeResult = await Task.Run(() => _brightnessControlService.SetBrightness(
                display,
                ddcProbe,
                wmiProbe,
                requestedPercent));
            _lastBrightnessWriteResult = writeResult;
            var refreshed = await Task.Run(() => (
                Ddc: _ddcProbeService.ProbeBrightness(_activeMonitors),
                Wmi: _wmiProbeService.ProbeBrightness(_activeMonitors)));
            _lastDdcProbes = refreshed.Ddc;
            _lastWmiProbes = refreshed.Wmi;
            UpdateMonitorCards(display.DevicePath);

            StatusText.Text = writeResult.Succeeded
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "Set {0} through {1}: requested {2}%, applied {3}%, verified {4}%.",
                    display.FriendlyName,
                    writeResult.Provider,
                    writeResult.RequestedPercent,
                    writeResult.AppliedPercent,
                    writeResult.VerifiedPercent)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "Brightness write did not verify ({0}, error 0x{1:X8}).",
                    writeResult.Status,
                    unchecked((uint)writeResult.ErrorCode));
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                refreshed.Ddc,
                refreshed.Wmi,
                writeResult,
                error: null);
        }
        catch (Win32Exception exception)
        {
            StatusText.Text = $"Brightness verification refresh failed: {exception.Message}";
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                _lastDdcProbes,
                _lastWmiProbes,
                writeResult,
                exception);
        }
        finally
        {
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            SetBrightnessButton.IsEnabled = CanSetSelectedDisplay();
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task ApplyThemeAsync(ThemeMode mode)
    {
        SetThemeButtonsEnabled(false);
        ThemeStatusText.Text = $"Applying {mode.ToString().ToLowerInvariant()} theme to apps and Windows...";

        try
        {
            _lastThemeResult = await Task.Run(() => _themeService.Apply(mode));
            _themeState = _lastThemeResult.After;
            UpdateThemeStatus(_lastThemeResult.Succeeded
                ? $"Applied and verified {mode.ToString().ToLowerInvariant()} theme."
                : $"Windows did not verify the requested {mode.ToString().ToLowerInvariant()} theme.");
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                _lastDdcProbes.Count == 0 ? null : _lastDdcProbes,
                _lastWmiProbes.Count == 0 ? null : _lastWmiProbes,
                _lastBrightnessWriteResult,
                error: null);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportThemeFailure(exception);
        }
        catch (SecurityException exception)
        {
            ReportThemeFailure(exception);
        }
        catch (IOException exception)
        {
            ReportThemeFailure(exception);
        }
        finally
        {
            SetThemeButtonsEnabled(true);
        }
    }

    private void RefreshThemeStatus()
    {
        try
        {
            _themeState = _themeService.ReadState();
            UpdateThemeStatus(prefix: null);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportThemeFailure(exception);
        }
        catch (SecurityException exception)
        {
            ReportThemeFailure(exception);
        }
        catch (IOException exception)
        {
            ReportThemeFailure(exception);
        }
    }

    private void RefreshSchedulePreview()
    {
        try
        {
            _customThemeSchedule = new CustomThemeSchedule(
                TimeOnly.FromTimeSpan(LightScheduleTimePicker.Time),
                TimeOnly.FromTimeSpan(DarkScheduleTimePicker.Time));
            _lastScheduleEvaluation = CustomThemeScheduleEvaluator.Evaluate(
                _customThemeSchedule,
                TimeOnly.FromDateTime(DateTime.Now));

            var remainingMinutes = (int)Math.Ceiling(_lastScheduleEvaluation.TimeUntilNextTransition.TotalMinutes);
            ScheduleStatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Now: {0}. Next: {1} at {2} ({3} minute(s)). Preview only; no automatic theme change.",
                _lastScheduleEvaluation.ActiveMode,
                _lastScheduleEvaluation.NextMode,
                FormatTime(_lastScheduleEvaluation.NextTransitionTime),
                remainingMinutes);
            RefreshDiagnosticReport();
        }
        catch (ArgumentException exception)
        {
            _customThemeSchedule = null;
            _lastScheduleEvaluation = null;
            ScheduleStatusText.Text = exception.Message;
            RefreshDiagnosticReport();
        }
    }

    private void LoadScheduleSettings()
    {
        try
        {
            var result = _themeScheduleSettingsStore.Load();
            LightScheduleTimePicker.Time = result.Schedule.LightTime.ToTimeSpan();
            DarkScheduleTimePicker.Time = result.Schedule.DarkTime.ToTimeSpan();
            _scheduleWasLoadedFromDisk = result.WasLoadedFromDisk;
            _scheduleSettingsError = null;
            SchedulePersistenceStatusText.Text = result.WasLoadedFromDisk
                ? "Loaded the saved per-user schedule."
                : "Using the default schedule; select Save schedule to persist it.";
        }
        catch (IOException exception)
        {
            ReportScheduleLoadFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportScheduleLoadFailure(exception);
        }
        catch (SecurityException exception)
        {
            ReportScheduleLoadFailure(exception);
        }
    }

    private void ReportScheduleLoadFailure(Exception exception)
    {
        var defaults = JsonThemeScheduleSettingsStore.CreateDefault();
        LightScheduleTimePicker.Time = defaults.LightTime.ToTimeSpan();
        DarkScheduleTimePicker.Time = defaults.DarkTime.ToTimeSpan();
        _scheduleWasLoadedFromDisk = false;
        _scheduleSettingsError = exception.GetType().Name;
        SchedulePersistenceStatusText.Text = "Saved schedule could not be loaded; using safe defaults.";
    }

    private void SaveScheduleSettings()
    {
        try
        {
            var schedule = new CustomThemeSchedule(
                TimeOnly.FromTimeSpan(LightScheduleTimePicker.Time),
                TimeOnly.FromTimeSpan(DarkScheduleTimePicker.Time));
            _themeScheduleSettingsStore.Save(schedule);
            _scheduleWasLoadedFromDisk = true;
            _scheduleSettingsError = null;
            SchedulePersistenceStatusText.Text = "Saved the per-user schedule.";
            RefreshSchedulePreview();
        }
        catch (ArgumentException)
        {
            RefreshSchedulePreview();
        }
        catch (IOException exception)
        {
            ReportScheduleSaveFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportScheduleSaveFailure(exception);
        }
        catch (SecurityException exception)
        {
            ReportScheduleSaveFailure(exception);
        }
    }

    private void ReportScheduleSaveFailure(Exception exception)
    {
        _scheduleSettingsError = exception.GetType().Name;
        SchedulePersistenceStatusText.Text = "Schedule settings could not be saved.";
        RefreshDiagnosticReport();
    }

    private void RefreshDiagnosticReport()
    {
        _diagnosticReport = BuildDiagnosticReport(
            _activeMonitors,
            _lastDdcProbes.Count == 0 ? null : _lastDdcProbes,
            _lastWmiProbes.Count == 0 ? null : _lastWmiProbes,
            _lastBrightnessWriteResult,
            error: null);
    }

    private static string FormatTime(TimeOnly time) =>
        DateTime.Today.Add(time.ToTimeSpan()).ToString("t", CultureInfo.CurrentCulture);

    private void UpdateThemeStatus(string? prefix)
    {
        if (_themeState is null)
        {
            ThemeStatusText.Text = prefix ?? "Theme state unavailable.";
            return;
        }

        var state = string.Format(
            CultureInfo.CurrentCulture,
            "Apps: {0} · Windows: {1}",
            _themeState.AppsUseLightTheme ? "Light" : "Dark",
            _themeState.SystemUsesLightTheme ? "Light" : "Dark");
        ThemeStatusText.Text = string.IsNullOrWhiteSpace(prefix) ? state : $"{prefix} {state}";
    }

    private void ReportThemeFailure(Exception exception)
    {
        ThemeStatusText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Theme operation failed (0x{0:X8}): {1}",
            exception.HResult,
            exception.Message);
    }

    private void SetThemeButtonsEnabled(bool enabled)
    {
        RefreshThemeButton.IsEnabled = enabled;
        ApplyLightThemeButton.IsEnabled = enabled;
        ApplyDarkThemeButton.IsEnabled = enabled;
    }

    private void UpdateMonitorCards(string? selectedDevicePath)
    {
        var cards = _lastDdcProbes.Select(ddcProbe =>
        {
            var wmiProbe = _lastWmiProbes.First(candidate => string.Equals(
                candidate.Display.DevicePath,
                ddcProbe.Display.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            return MonitorCardViewModel.FromProbes(ddcProbe, wmiProbe);
        }).ToArray();
        MonitorList.ItemsSource = cards;
        var selectedCard = selectedDevicePath is null
            ? null
            : cards.FirstOrDefault(card => string.Equals(
                card.DevicePath,
                selectedDevicePath,
                StringComparison.OrdinalIgnoreCase));
        if (selectedCard is null)
        {
            var writableCards = cards.Where(card => HasValidatedWritePath(card.DevicePath)).ToArray();
            selectedCard = writableCards.Length == 1 ? writableCards[0] : null;
        }

        MonitorList.SelectedItem = selectedCard;
    }

    private bool CanSetSelectedDisplay()
    {
        if (MonitorList.SelectedItem is not MonitorCardViewModel selected)
        {
            return false;
        }

        return HasValidatedWritePath(selected.DevicePath);
    }

    private bool HasValidatedWritePath(string devicePath)
    {
        return _lastWmiProbes.Any(probe =>
                string.Equals(probe.Display.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)
                && probe.Status == WmiBrightnessProbeStatus.ReadSucceeded)
            || _lastDdcProbes.Any(probe =>
                string.Equals(probe.Display.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)
                && probe.PhysicalMonitors.Any(result =>
                    result.Status == DdcBrightnessProbeStatus.ReadSucceeded));
    }

    private string BuildDiagnosticReport(
        IReadOnlyList<MonitorDisplayInfo> monitors,
        IReadOnlyList<MonitorDdcProbeInfo>? ddcProbes,
        IReadOnlyList<WmiBrightnessProbeResult>? wmiProbes,
        BrightnessWriteResult? writeResult,
        Win32Exception? error)
    {
        var report = new StringBuilder();
        report.AppendLine("DisplayPilot active display-path report");
        report.Append("Captured: ").AppendLine(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        report.Append("OS: ").AppendLine(RuntimeInformation.OSDescription);
        report.Append("Process architecture: ").AppendLine(RuntimeInformation.ProcessArchitecture.ToString());
        report.Append("Display paths: ").AppendLine(monitors.Count.ToString(CultureInfo.InvariantCulture));
        report.Append("Theme apps: ").AppendLine(_themeState is null ? "Unknown" : _themeState.AppsUseLightTheme ? "Light" : "Dark");
        report.Append("Theme Windows: ").AppendLine(_themeState is null ? "Unknown" : _themeState.SystemUsesLightTheme ? "Light" : "Dark");
        report.Append("Last theme request: ").AppendLine(_lastThemeResult?.RequestedMode.ToString() ?? "None");
        report.Append("Last theme request verified: ").AppendLine(_lastThemeResult?.Succeeded.ToString() ?? "Not applicable");
        report.Append("Schedule light time: ").AppendLine(_customThemeSchedule is null ? "Invalid" : _customThemeSchedule.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        report.Append("Schedule dark time: ").AppendLine(_customThemeSchedule is null ? "Invalid" : _customThemeSchedule.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        report.Append("Schedule preview mode: ").AppendLine(_lastScheduleEvaluation?.ActiveMode.ToString() ?? "Unavailable");
        report.Append("Schedule next mode: ").AppendLine(_lastScheduleEvaluation?.NextMode.ToString() ?? "Unavailable");
        report.Append("Schedule persisted: ").AppendLine(_scheduleWasLoadedFromDisk.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule settings error: ").AppendLine(_scheduleSettingsError ?? "None");
        report.Append("Schedule automatic writes enabled: false").AppendLine();
        report.AppendLine("Privacy: device paths and WMI instance names can identify a local display instance; review before sharing");
        report.AppendLine(ddcProbes is null
            ? "DDC/CI commands issued: no"
            : writeResult?.Provider == BrightnessWriteProvider.DdcCi
                ? "DDC/CI commands issued: brightness VCP 0x10 read, write, and verification read-back"
                : "DDC/CI commands issued: read-only brightness VCP 0x10 queries; no DDC writes");
        report.AppendLine(wmiProbes is null
            ? "WMI commands issued: no"
            : writeResult?.Provider == BrightnessWriteProvider.Wmi
                ? "WMI commands issued: WmiSetBrightness and read-only verification query"
                : "WMI commands issued: read-only WmiMonitorBrightness query; no WMI method calls");

        if (writeResult is not null)
        {
            report.Append("Brightness write provider: ").AppendLine(writeResult.Provider.ToString());
            report.Append("Brightness write status: ").AppendLine(writeResult.Status.ToString());
            report.Append("Brightness requested percent: ").AppendLine(writeResult.RequestedPercent.ToString(CultureInfo.InvariantCulture));
            report.Append("Brightness applied percent: ").AppendLine(writeResult.AppliedPercent.ToString(CultureInfo.InvariantCulture));
            report.Append("Brightness verified percent: ").AppendLine(writeResult.VerifiedPercent.ToString(CultureInfo.InvariantCulture));
            report.Append("Brightness write error: ")
                .Append(writeResult.ErrorCode.ToString(CultureInfo.InvariantCulture))
                .Append(" / 0x")
                .AppendLine(unchecked((uint)writeResult.ErrorCode).ToString("X8", CultureInfo.InvariantCulture));
            report.Append("Brightness write message: ").AppendLine(writeResult.Message);
        }

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
            }
            else
            {
                foreach (var physicalMonitor in probe.PhysicalMonitors)
                {
                    report.Append("DDC/CI status: ").AppendLine(physicalMonitor.Status.ToString());
                    report.Append("Physical description: ").AppendLine(physicalMonitor.PhysicalMonitorDescription);
                    report.Append("DDC brightness current: ").AppendLine(physicalMonitor.CurrentValue.ToString(CultureInfo.InvariantCulture));
                    report.Append("DDC brightness maximum: ").AppendLine(physicalMonitor.MaximumValue.ToString(CultureInfo.InvariantCulture));
                    report.Append("Handle acquisition attempts: ").AppendLine(physicalMonitor.HandleAcquisitionAttempts.ToString(CultureInfo.InvariantCulture));
                    report.Append("VCP read attempts: ").AppendLine(physicalMonitor.AttemptCount.ToString(CultureInfo.InvariantCulture));
                    report.Append("Win32 error: ")
                        .Append(physicalMonitor.Win32Error.ToString(CultureInfo.InvariantCulture))
                        .Append(" / 0x")
                        .AppendLine(unchecked((uint)physicalMonitor.Win32Error).ToString("X8", CultureInfo.InvariantCulture));
                }
            }

            var wmiProbe = wmiProbes?.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                monitor.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            if (wmiProbe is null)
            {
                report.AppendLine("WMI: not probed");
                continue;
            }

            report.Append("WMI status: ").AppendLine(wmiProbe.Status.ToString());
            report.Append("WMI instance name: ").AppendLine(wmiProbe.InstanceName);
            report.Append("WMI brightness current: ").AppendLine(wmiProbe.CurrentBrightness.ToString(CultureInfo.InvariantCulture));
            report.Append("WMI brightness level count: ").AppendLine(wmiProbe.LevelCount.ToString(CultureInfo.InvariantCulture));
            report.Append("WMI error: ")
                .Append(wmiProbe.ErrorCode.ToString(CultureInfo.InvariantCulture))
                .Append(" / 0x")
                .AppendLine(unchecked((uint)wmiProbe.ErrorCode).ToString("X8", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(wmiProbe.ErrorMessage))
            {
                report.Append("WMI error message: ").AppendLine(wmiProbe.ErrorMessage);
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
