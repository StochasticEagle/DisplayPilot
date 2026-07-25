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
using DisplayPilot.Windows.Scheduling;
using DisplayPilot.Windows.Settings;
using DisplayPilot.Windows.Shell;
using DisplayPilot.Windows.Theme;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace DisplayPilot.App;

public sealed partial class MainWindow : Window, IDisposable
{
    private const int CompactWidth = 480;
    private const int CompactHeight = 520;
    private const int AdvancedWidth = 900;
    private const int AdvancedHeight = 860;
    private static readonly long CompactReopenDelayMilliseconds =
        Math.Max(GetDoubleClickTime(), 1u);
    private readonly IMonitorDiscoveryService _monitorDiscovery = new DisplayConfigMonitorDiscovery();
    private readonly DdcBrightnessProbeService _ddcProbeService = new();
    private readonly WmiBrightnessProbeService _wmiProbeService = new();
    private readonly BrightnessControlService _brightnessControlService = new();
    private readonly WindowsThemeService _themeService = new();
    private readonly JsonThemeScheduleSettingsStore _themeScheduleSettingsStore = new();
    private readonly WindowsBoundaryTimer _themeScheduleTimer = new();
    private IReadOnlyList<MonitorDisplayInfo> _activeMonitors = [];
    private IReadOnlyList<MonitorDdcProbeInfo> _lastDdcProbes = [];
    private IReadOnlyList<WmiBrightnessProbeResult> _lastWmiProbes = [];
    private ThemeState? _themeState;
    private ThemeApplyResult? _lastThemeResult;
    private CustomThemeSchedule? _customThemeSchedule;
    private CustomThemeSchedule? _savedThemeSchedule;
    private ThemeScheduleEvaluation? _lastScheduleEvaluation;
    private bool _scheduleWasLoadedFromDisk;
    private bool _scheduleAutomationEnabled;
    private DateTimeOffset? _manualScheduleOverrideUntil;
    private string? _scheduleSettingsError;
    private BrightnessWriteResult? _lastBrightnessWriteResult;
    private bool _themeOperationRunning;
    private bool _displayOperationRunning;
    private bool _initialScanStarted;
    private bool _updatingCompactControls;
    private bool _isCompactMode = true;
    private long _compactShowBlockedUntil;
    private bool _exitRequested;
    private bool _disposed;
    private NotificationAreaIcon? _notificationAreaIcon;
    private string _diagnosticReport = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureCompactWindow();
        _themeScheduleTimer.Elapsed += ThemeScheduleTimer_Elapsed;
        Activated += MainWindow_Activated;
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        LoadScheduleSettings();
        RefreshSchedulePreview();
        SystemText.Text = GetSystemSummary();
    }

    public bool InitializeNotificationArea()
    {
        try
        {
            var window = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _notificationAreaIcon = new NotificationAreaIcon(window);
            _notificationAreaIcon.PrimaryInvoked += NotificationAreaIcon_PrimaryInvoked;
            _notificationAreaIcon.OpenInvoked += NotificationAreaIcon_OpenInvoked;
            _notificationAreaIcon.AdvancedInvoked += NotificationAreaIcon_AdvancedInvoked;
            _notificationAreaIcon.ExitInvoked += NotificationAreaIcon_ExitInvoked;
            return true;
        }
        catch (Win32Exception exception)
        {
            CompactStatusText.Text = $"Notification-area icon unavailable: {exception.Message}";
            return false;
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialScanStarted)
        {
            return;
        }

        _initialScanStarted = true;
        RefreshThemeStatus();
        await EvaluateAndApplyScheduleAsync();
        UpdateThemeScheduleTimer();
        await RefreshDisplaysAsync();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private async void NotificationAreaIcon_PrimaryInvoked(object? sender, EventArgs e)
    {
        if (AppWindow.IsVisible)
        {
            HideCompactViewAndBlockImmediateReopen();
            return;
        }

        if (Environment.TickCount64 < _compactShowBlockedUntil)
        {
            return;
        }

        ShowCompactView();
        if (_activeMonitors.Count > 0 && _lastDdcProbes.Count == 0 && _lastWmiProbes.Count == 0)
        {
            await ProbeDdcBrightnessAsync();
        }
    }

    private void NotificationAreaIcon_AdvancedInvoked(object? sender, EventArgs e)
    {
        ShowAdvancedView();
    }

    private async void NotificationAreaIcon_OpenInvoked(object? sender, EventArgs e)
    {
        ShowCompactView();
        if (_activeMonitors.Count > 0 && _lastDdcProbes.Count == 0 && _lastWmiProbes.Count == 0)
        {
            await ProbeDdcBrightnessAsync();
        }
    }

    private void NotificationAreaIcon_ExitInvoked(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void ShowAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        ShowAdvancedView();
    }

    private void ShowCompactButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCompactView();
    }

    private void HideCompactButton_Click(object sender, RoutedEventArgs e)
    {
        HideCompactViewAndBlockImmediateReopen();
    }

    private async void CompactReadBrightnessButton_Click(object sender, RoutedEventArgs e)
    {
        await ProbeDdcBrightnessAsync();
    }

    private async void CompactSetBrightnessButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CompactMonitorViewModel monitor })
        {
            await SetBrightnessAsync(
                monitor.DevicePath,
                Math.Clamp((int)Math.Round(monitor.BrightnessPercent), 0, 100));
        }
    }

    private async void CompactDarkModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingCompactControls)
        {
            return;
        }

        var mode = CompactDarkModeToggle.IsOn ? ThemeMode.Dark : ThemeMode.Light;
        if (await ApplyThemeAsync(mode, isScheduledChange: false))
        {
            ActivateManualScheduleOverride(mode);
        }
    }

    private async void CompactScheduleAutomationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingCompactControls)
        {
            return;
        }

        ScheduleAutomationToggle.IsOn = CompactScheduleAutomationToggle.IsOn;
        if (SaveScheduleSettings())
        {
            await EvaluateAndApplyScheduleAsync();
            UpdateThemeScheduleTimer();
        }
    }

    private void RefreshThemeButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshThemeStatus();
    }

    private async void ApplyLightThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ApplyThemeAsync(ThemeMode.Light, isScheduledChange: false))
        {
            ActivateManualScheduleOverride(ThemeMode.Light);
        }
    }

    private async void ApplyDarkThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ApplyThemeAsync(ThemeMode.Dark, isScheduledChange: false))
        {
            ActivateManualScheduleOverride(ThemeMode.Dark);
        }
    }

    private void PreviewScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSchedulePreview();
    }

    private async void SaveScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        if (SaveScheduleSettings())
        {
            await EvaluateAndApplyScheduleAsync();
            UpdateThemeScheduleTimer();
        }
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
        if (_displayOperationRunning)
        {
            return;
        }

        _displayOperationRunning = true;
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        SetBrightnessButton.IsEnabled = false;
        CompactReadBrightnessButton.IsEnabled = false;
        CompactMonitorList.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Scanning active Windows display paths...";
        CompactStatusText.Text = "Scanning active Windows display paths...";
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            var monitors = await Task.Run(_monitorDiscovery.DiscoverActiveMonitors);

            _activeMonitors = monitors;
            _lastDdcProbes = [];
            _lastWmiProbes = [];
            MonitorList.ItemsSource = monitors.Select(MonitorCardViewModel.NotProbed).ToArray();
            UpdateCompactMonitorCards();
            EmptyState.Visibility = monitors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Found {0} active display {1} at {2:T}.",
                monitors.Count,
                monitors.Count == 1 ? "path" : "paths",
                DateTimeOffset.Now);
            CompactStatusText.Text = monitors.Count == 0
                ? "No active displays were found."
                : "Open the controls to read current brightness.";
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
            UpdateCompactMonitorCards();
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = $"Display discovery failed: {exception.Message}";
            CompactStatusText.Text = "Display discovery failed. Open Advanced for details.";
            _diagnosticReport = BuildDiagnosticReport(
                [],
                ddcProbes: null,
                wmiProbes: null,
                writeResult: null,
                error: exception);
        }
        finally
        {
            _displayOperationRunning = false;
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            CompactReadBrightnessButton.IsEnabled = _activeMonitors.Count > 0;
            CompactMonitorList.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task ProbeDdcBrightnessAsync()
    {
        if (_displayOperationRunning)
        {
            return;
        }

        _displayOperationRunning = true;
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        SetBrightnessButton.IsEnabled = false;
        CompactReadBrightnessButton.IsEnabled = false;
        CompactMonitorList.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Reading external DDC/CI and internal WMI brightness...";
        CompactStatusText.Text = "Reading monitor brightness...";

        try
        {
            var probes = await Task.Run(() => (
                Ddc: _ddcProbeService.ProbeBrightness(_activeMonitors),
                Wmi: _wmiProbeService.ProbeBrightness(_activeMonitors)));
            _lastDdcProbes = probes.Ddc;
            _lastWmiProbes = probes.Wmi;
            UpdateMonitorCards(selectedDevicePath: null);
            UpdateCompactMonitorCards();

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
            CompactStatusText.Text = readableCount == 0
                ? "Brightness control is unavailable. Open Advanced for details."
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "Brightness is available for {0} display {1}.",
                    readableCount,
                    readableCount == 1 ? "path" : "paths");
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
            CompactStatusText.Text = "Brightness refresh failed. Open Advanced for details.";
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                ddcProbes: null,
                wmiProbes: null,
                writeResult: null,
                error: exception);
        }
        finally
        {
            _displayOperationRunning = false;
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            SetBrightnessButton.IsEnabled = CanSetSelectedDisplay();
            CompactReadBrightnessButton.IsEnabled = _activeMonitors.Count > 0;
            CompactMonitorList.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task SetSelectedBrightnessAsync()
    {
        if (MonitorList.SelectedItem is not MonitorCardViewModel selected)
        {
            return;
        }

        var requestedPercent = double.IsNaN(BrightnessValue.Value)
            ? 50
            : Math.Clamp((int)Math.Round(BrightnessValue.Value), 0, 100);
        await SetBrightnessAsync(selected.DevicePath, requestedPercent);
    }

    private async Task SetBrightnessAsync(string devicePath, int requestedPercent)
    {
        if (_displayOperationRunning)
        {
            return;
        }

        var display = _activeMonitors.First(candidate => string.Equals(
            candidate.DevicePath,
            devicePath,
            StringComparison.OrdinalIgnoreCase));
        var ddcProbe = _lastDdcProbes.First(candidate => string.Equals(
            candidate.Display.DevicePath,
            display.DevicePath,
            StringComparison.OrdinalIgnoreCase));
        var wmiProbe = _lastWmiProbes.First(candidate => string.Equals(
            candidate.Display.DevicePath,
            display.DevicePath,
            StringComparison.OrdinalIgnoreCase));

        _displayOperationRunning = true;
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        SetBrightnessButton.IsEnabled = false;
        CompactReadBrightnessButton.IsEnabled = false;
        CompactMonitorList.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        StatusText.Text = $"Setting {display.FriendlyName} brightness to {requestedPercent}%...";
        CompactStatusText.Text = $"Setting {display.FriendlyName} to {requestedPercent}%...";
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
            UpdateCompactMonitorCards();

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
            CompactStatusText.Text = writeResult.Succeeded
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}: verified at {1}%.",
                    display.FriendlyName,
                    writeResult.VerifiedPercent)
                : "Brightness did not verify. Open Advanced for details.";
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
            CompactStatusText.Text = "Brightness verification failed. Open Advanced for details.";
            _diagnosticReport = BuildDiagnosticReport(
                _activeMonitors,
                _lastDdcProbes,
                _lastWmiProbes,
                writeResult,
                exception);
        }
        finally
        {
            _displayOperationRunning = false;
            RescanButton.IsEnabled = true;
            ProbeDdcButton.IsEnabled = _activeMonitors.Count > 0;
            SetBrightnessButton.IsEnabled = CanSetSelectedDisplay();
            CompactReadBrightnessButton.IsEnabled = _activeMonitors.Count > 0;
            CompactMonitorList.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task<bool> ApplyThemeAsync(ThemeMode mode, bool isScheduledChange)
    {
        if (_themeOperationRunning)
        {
            return false;
        }

        _themeOperationRunning = true;
        SetThemeButtonsEnabled(false);
        ThemeStatusText.Text = isScheduledChange
            ? $"Schedule is applying {mode.ToString().ToLowerInvariant()} theme to apps and Windows..."
            : $"Applying {mode.ToString().ToLowerInvariant()} theme to apps and Windows...";
        var succeeded = false;

        try
        {
            _lastThemeResult = await Task.Run(() => _themeService.Apply(mode));
            _themeState = _lastThemeResult.After;
            succeeded = _lastThemeResult.Succeeded;
            UpdateThemeStatus(_lastThemeResult.Succeeded
                ? isScheduledChange
                    ? $"Schedule applied and verified {mode.ToString().ToLowerInvariant()} theme."
                    : $"Applied and verified {mode.ToString().ToLowerInvariant()} theme."
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
            _themeOperationRunning = false;
            SetThemeButtonsEnabled(true);
        }

        return succeeded;
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
            var automationStatus = _scheduleAutomationEnabled
                ? "Automatic switching is enabled while DisplayPilot is running."
                : "Preview only; automatic switching is disabled.";
            ScheduleStatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Selected: Light {0} · Dark {1}. Now: {2}. Next: {3} at {4} ({5} minute(s)). {6}",
                _customThemeSchedule.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                _customThemeSchedule.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                _lastScheduleEvaluation.ActiveMode,
                _lastScheduleEvaluation.NextMode,
                FormatTime(_lastScheduleEvaluation.NextTransitionTime),
                remainingMinutes,
                automationStatus);
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
            _savedThemeSchedule = result.Schedule;
            _scheduleWasLoadedFromDisk = result.WasLoadedFromDisk;
            _scheduleAutomationEnabled = result.AutomationEnabled;
            _updatingCompactControls = true;
            try
            {
                ScheduleAutomationToggle.IsOn = result.AutomationEnabled;
                CompactScheduleAutomationToggle.IsOn = result.AutomationEnabled;
            }
            finally
            {
                _updatingCompactControls = false;
            }

            _scheduleSettingsError = null;
            SchedulePersistenceStatusText.Text = result.WasLoadedFromDisk
                ? result.AutomationEnabled
                    ? "Loaded the saved schedule; automatic switching is enabled while the app runs."
                    : "Loaded the saved per-user schedule; automatic switching is disabled."
                : "Using the default schedule; select Save schedule to persist it.";
            UpdateCompactScheduleStatus();
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
        _savedThemeSchedule = defaults;
        _scheduleWasLoadedFromDisk = false;
        _scheduleAutomationEnabled = false;
        _updatingCompactControls = true;
        try
        {
            ScheduleAutomationToggle.IsOn = false;
            CompactScheduleAutomationToggle.IsOn = false;
        }
        finally
        {
            _updatingCompactControls = false;
        }

        _scheduleSettingsError = exception.GetType().Name;
        SchedulePersistenceStatusText.Text = "Saved schedule could not be loaded; using safe defaults.";
        UpdateCompactScheduleStatus();
    }

    private bool SaveScheduleSettings()
    {
        try
        {
            var schedule = new CustomThemeSchedule(
                TimeOnly.FromTimeSpan(LightScheduleTimePicker.Time),
                TimeOnly.FromTimeSpan(DarkScheduleTimePicker.Time));
            var automationEnabled = ScheduleAutomationToggle.IsOn;
            _themeScheduleSettingsStore.Save(schedule, automationEnabled);
            _savedThemeSchedule = schedule;
            _scheduleAutomationEnabled = automationEnabled;
            _updatingCompactControls = true;
            try
            {
                CompactScheduleAutomationToggle.IsOn = automationEnabled;
            }
            finally
            {
                _updatingCompactControls = false;
            }

            _scheduleWasLoadedFromDisk = true;
            _manualScheduleOverrideUntil = null;
            _scheduleSettingsError = null;
            SchedulePersistenceStatusText.Text = _scheduleAutomationEnabled
                ? "Saved the schedule; automatic switching is enabled while the app runs."
                : "Saved the schedule; automatic switching is disabled.";
            RefreshSchedulePreview();
            UpdateCompactScheduleStatus();
            return true;
        }
        catch (ArgumentException)
        {
            RefreshSchedulePreview();
            return false;
        }
        catch (IOException exception)
        {
            ReportScheduleSaveFailure(exception);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportScheduleSaveFailure(exception);
            return false;
        }
        catch (SecurityException exception)
        {
            ReportScheduleSaveFailure(exception);
            return false;
        }
    }

    private void ReportScheduleSaveFailure(Exception exception)
    {
        _scheduleSettingsError = exception.GetType().Name;
        SchedulePersistenceStatusText.Text = "Schedule settings could not be saved.";
        RefreshDiagnosticReport();
    }

    private void ThemeScheduleTimer_Elapsed(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            await EvaluateAndApplyScheduleAsync();
            UpdateThemeScheduleTimer();
            UpdateCompactScheduleStatus();
        });
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (_isCompactMode && AppWindow.IsVisible)
            {
                HideCompactViewAndBlockImmediateReopen();
            }

            return;
        }

        if (!_initialScanStarted)
        {
            return;
        }

        await EvaluateAndApplyScheduleAsync();
        UpdateThemeScheduleTimer();
        UpdateCompactScheduleStatus();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Activated -= MainWindow_Activated;
        AppWindow.Closing -= AppWindow_Closing;
        Closed -= MainWindow_Closed;
        _themeScheduleTimer.Elapsed -= ThemeScheduleTimer_Elapsed;
        if (_notificationAreaIcon is not null)
        {
            _notificationAreaIcon.PrimaryInvoked -= NotificationAreaIcon_PrimaryInvoked;
            _notificationAreaIcon.OpenInvoked -= NotificationAreaIcon_OpenInvoked;
            _notificationAreaIcon.AdvancedInvoked -= NotificationAreaIcon_AdvancedInvoked;
            _notificationAreaIcon.ExitInvoked -= NotificationAreaIcon_ExitInvoked;
            _notificationAreaIcon.Dispose();
        }

        _themeScheduleTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ShowCompactView()
    {
        ConfigureCompactWindow();
        PositionCompactWindow();
        AppWindow.Show();
        Activate();
        _ = _notificationAreaIcon?.TryBringWindowToForeground();
    }

    private void HideCompactViewAndBlockImmediateReopen()
    {
        _compactShowBlockedUntil = Environment.TickCount64 + CompactReopenDelayMilliseconds;
        AppWindow.Hide();
    }

    private void ShowAdvancedView()
    {
        _isCompactMode = false;
        CompactView.Visibility = Visibility.Collapsed;
        AdvancedView.Visibility = Visibility.Visible;
        AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.IsResizable = true;
        }

        AppWindow.IsShownInSwitchers = true;
        MoveAndResizeAdvancedWindow();
        AppWindow.Show();
        Activate();
    }

    private void ConfigureCompactWindow()
    {
        _isCompactMode = true;
        CompactView.Visibility = Visibility.Visible;
        AdvancedView.Visibility = Visibility.Collapsed;
        AppWindow.SetPresenter(OverlappedPresenter.CreateForContextMenu());
        AppWindow.IsShownInSwitchers = false;

        var window = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = WindowWorkArea.GetScale(window);
        AppWindow.Resize(new SizeInt32(
            (int)Math.Round(CompactWidth * scale),
            (int)Math.Round(CompactHeight * scale)));
    }

    private void PositionCompactWindow()
    {
        if (_notificationAreaIcon is null ||
            !_notificationAreaIcon.TryGetBounds(out var iconBounds) ||
            !WindowWorkArea.TryGetNearest(iconBounds, out var workArea))
        {
            return;
        }

        AppWindow.Move(new PointInt32(iconBounds.Left, workArea.Top));
        var window = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = WindowWorkArea.GetScale(window);
        var width = Math.Min(
            (int)Math.Round(CompactWidth * scale),
            workArea.Right - workArea.Left);
        var height = Math.Min(
            (int)Math.Round(CompactHeight * scale),
            workArea.Bottom - workArea.Top);
        AppWindow.Resize(new SizeInt32(width, height));
        var placement = FlyoutPlacement.Calculate(
            iconBounds,
            workArea,
            width,
            height);
        AppWindow.Move(new PointInt32(placement.Left, placement.Top));
    }

    private void MoveAndResizeAdvancedWindow()
    {
        var window = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = WindowWorkArea.GetScale(window);
        var width = (int)Math.Round(AdvancedWidth * scale);
        var height = (int)Math.Round(AdvancedHeight * scale);

        if (_notificationAreaIcon is not null &&
            _notificationAreaIcon.TryGetBounds(out var iconBounds) &&
            WindowWorkArea.TryGetNearest(iconBounds, out var workArea))
        {
            width = Math.Min(width, workArea.Right - workArea.Left);
            height = Math.Min(height, workArea.Bottom - workArea.Top);
            var x = workArea.Left + ((workArea.Right - workArea.Left - width) / 2);
            var y = workArea.Top + ((workArea.Bottom - workArea.Top - height) / 2);
            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
            return;
        }

        AppWindow.Resize(new SizeInt32(width, height));
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    private async Task EvaluateAndApplyScheduleAsync()
    {
        if (!_scheduleAutomationEnabled || _themeOperationRunning)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (_manualScheduleOverrideUntil is not null && now < _manualScheduleOverrideUntil)
        {
            SchedulePersistenceStatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Manual theme override is active until {0:t}.",
                _manualScheduleOverrideUntil);
            return;
        }

        if (_manualScheduleOverrideUntil is not null)
        {
            _manualScheduleOverrideUntil = null;
            SchedulePersistenceStatusText.Text = "The schedule boundary ended the manual override.";
        }

        if (_savedThemeSchedule is null)
        {
            return;
        }

        var scheduledEvaluation = CustomThemeScheduleEvaluator.Evaluate(
            _savedThemeSchedule,
            TimeOnly.FromDateTime(now.LocalDateTime));

        RefreshThemeStatus();
        if (_themeState is null)
        {
            return;
        }

        var shouldBeLight = scheduledEvaluation.ActiveMode == ThemeMode.Light;
        if (_themeState.AppsUseLightTheme != shouldBeLight ||
            _themeState.SystemUsesLightTheme != shouldBeLight)
        {
            _ = await ApplyThemeAsync(scheduledEvaluation.ActiveMode, isScheduledChange: true);
        }
    }

    private void ActivateManualScheduleOverride(ThemeMode appliedMode)
    {
        if (!_scheduleAutomationEnabled)
        {
            return;
        }

        if (_savedThemeSchedule is null)
        {
            _manualScheduleOverrideUntil = null;
            return;
        }

        var now = DateTimeOffset.Now;
        var scheduledEvaluation = CustomThemeScheduleEvaluator.Evaluate(
            _savedThemeSchedule,
            TimeOnly.FromDateTime(now.LocalDateTime));
        if (appliedMode == scheduledEvaluation.ActiveMode)
        {
            _manualScheduleOverrideUntil = null;
            return;
        }

        _manualScheduleOverrideUntil = now.Add(scheduledEvaluation.TimeUntilNextTransition);
        SchedulePersistenceStatusText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Manual theme override is active until {0:t}.",
            _manualScheduleOverrideUntil);
        UpdateThemeScheduleTimer();
        UpdateCompactScheduleStatus();
        RefreshDiagnosticReport();
    }

    private void UpdateThemeScheduleTimer()
    {
        if (!_scheduleAutomationEnabled || !_initialScanStarted || _savedThemeSchedule is null)
        {
            _themeScheduleTimer.Cancel();
            return;
        }

        if (_themeOperationRunning)
        {
            _themeScheduleTimer.Arm(DateTimeOffset.Now.AddSeconds(1));
            return;
        }

        var now = DateTimeOffset.Now;
        var evaluation = CustomThemeScheduleEvaluator.Evaluate(
            _savedThemeSchedule,
            TimeOnly.FromDateTime(now.LocalDateTime));
        _themeScheduleTimer.Arm(now.Add(evaluation.TimeUntilNextTransition));
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
            CompactThemeStatusText.Text = "Theme state unavailable";
            return;
        }

        var state = string.Format(
            CultureInfo.CurrentCulture,
            "Apps: {0} · Windows: {1}",
            _themeState.AppsUseLightTheme ? "Light" : "Dark",
            _themeState.SystemUsesLightTheme ? "Light" : "Dark");
        ThemeStatusText.Text = string.IsNullOrWhiteSpace(prefix) ? state : $"{prefix} {state}";
        CompactThemeStatusText.Text =
            _themeState.AppsUseLightTheme == _themeState.SystemUsesLightTheme
                ? _themeState.AppsUseLightTheme ? "Windows is using Light mode" : "Windows is using Dark mode"
                : "Apps and Windows use mixed modes";
        _updatingCompactControls = true;
        try
        {
            CompactDarkModeToggle.IsOn =
                !_themeState.AppsUseLightTheme && !_themeState.SystemUsesLightTheme;
        }
        finally
        {
            _updatingCompactControls = false;
        }
    }

    private void ReportThemeFailure(Exception exception)
    {
        ThemeStatusText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Theme operation failed (0x{0:X8}): {1}",
            exception.HResult,
            exception.Message);
        CompactThemeStatusText.Text = "Theme operation failed. Open Advanced for details.";
    }

    private void SetThemeButtonsEnabled(bool enabled)
    {
        RefreshThemeButton.IsEnabled = enabled;
        ApplyLightThemeButton.IsEnabled = enabled;
        ApplyDarkThemeButton.IsEnabled = enabled;
        CompactDarkModeToggle.IsEnabled = enabled;
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

    private void UpdateCompactMonitorCards()
    {
        CompactMonitorList.ItemsSource = _activeMonitors.Select(display =>
        {
            var wmiProbe = _lastWmiProbes.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                display.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            if (wmiProbe?.Status == WmiBrightnessProbeStatus.ReadSucceeded)
            {
                return new CompactMonitorViewModel(
                    display.DevicePath,
                    display.FriendlyName,
                    isBrightnessAvailable: true,
                    wmiProbe.CurrentBrightness,
                    "Internal display · WMI");
            }

            var ddcProbe = _lastDdcProbes.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                display.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            var successfulRead = ddcProbe?.PhysicalMonitors.FirstOrDefault(result =>
                result.Status == DdcBrightnessProbeStatus.ReadSucceeded &&
                result.MaximumValue > 0);
            if (successfulRead is not null)
            {
                var percent = Math.Clamp(
                    successfulRead.CurrentValue * 100d / successfulRead.MaximumValue,
                    0,
                    100);
                return new CompactMonitorViewModel(
                    display.DevicePath,
                    display.FriendlyName,
                    isBrightnessAvailable: true,
                    percent,
                    "External display · DDC/CI");
            }

            return new CompactMonitorViewModel(
                display.DevicePath,
                display.FriendlyName,
                isBrightnessAvailable: false,
                brightnessPercent: 0,
                _lastDdcProbes.Count == 0 && _lastWmiProbes.Count == 0
                    ? "Brightness has not been read yet"
                    : "Brightness control unavailable");
        }).ToArray();
    }

    private void UpdateCompactScheduleStatus()
    {
        if (_savedThemeSchedule is null)
        {
            CompactScheduleStatusText.Text = "No valid saved schedule";
            return;
        }

        if (_manualScheduleOverrideUntil is not null &&
            DateTimeOffset.Now < _manualScheduleOverrideUntil)
        {
            CompactScheduleStatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Manual override until {0:t}",
                _manualScheduleOverrideUntil);
            return;
        }

        var evaluation = CustomThemeScheduleEvaluator.Evaluate(
            _savedThemeSchedule,
            TimeOnly.FromDateTime(DateTime.Now));
        CompactScheduleStatusText.Text = _scheduleAutomationEnabled
            ? string.Format(
                CultureInfo.CurrentCulture,
                "On · {0} at {1}",
                evaluation.NextMode,
                FormatTime(evaluation.NextTransitionTime))
            : string.Format(
                CultureInfo.CurrentCulture,
                "Off · Light {0}, Dark {1}",
                FormatTime(_savedThemeSchedule.LightTime),
                FormatTime(_savedThemeSchedule.DarkTime));
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
        report.Append("Notification-area icon active: ").AppendLine((_notificationAreaIcon is not null).ToString(CultureInfo.InvariantCulture));
        report.Append("Window mode: ").AppendLine(_isCompactMode ? "Compact" : "Advanced");
        report.Append("Window visible: ").AppendLine(AppWindow.IsVisible.ToString(CultureInfo.InvariantCulture));
        report.Append("Display paths: ").AppendLine(monitors.Count.ToString(CultureInfo.InvariantCulture));
        report.Append("Theme apps: ").AppendLine(_themeState is null ? "Unknown" : _themeState.AppsUseLightTheme ? "Light" : "Dark");
        report.Append("Theme Windows: ").AppendLine(_themeState is null ? "Unknown" : _themeState.SystemUsesLightTheme ? "Light" : "Dark");
        report.Append("Last theme request: ").AppendLine(_lastThemeResult?.RequestedMode.ToString() ?? "None");
        report.Append("Last theme request verified: ").AppendLine(_lastThemeResult?.Succeeded.ToString() ?? "Not applicable");
        report.Append("Schedule light time: ").AppendLine(_customThemeSchedule is null ? "Invalid" : _customThemeSchedule.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        report.Append("Schedule dark time: ").AppendLine(_customThemeSchedule is null ? "Invalid" : _customThemeSchedule.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        report.Append("Saved schedule light time: ").AppendLine(_savedThemeSchedule?.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Unavailable");
        report.Append("Saved schedule dark time: ").AppendLine(_savedThemeSchedule?.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Unavailable");
        report.Append("Schedule preview mode: ").AppendLine(_lastScheduleEvaluation?.ActiveMode.ToString() ?? "Unavailable");
        report.Append("Schedule next mode: ").AppendLine(_lastScheduleEvaluation?.NextMode.ToString() ?? "Unavailable");
        report.Append("Schedule persisted: ").AppendLine(_scheduleWasLoadedFromDisk.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule settings error: ").AppendLine(_scheduleSettingsError ?? "None");
        report.Append("Schedule automatic writes enabled: ").AppendLine(_scheduleAutomationEnabled.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule timer active: ").AppendLine(_themeScheduleTimer.IsArmed.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule timer due: ").AppendLine(_themeScheduleTimer.DueTime?.ToString("O", CultureInfo.InvariantCulture) ?? "None");
        report.Append("Schedule manual override until: ").AppendLine(_manualScheduleOverrideUntil?.ToString("O", CultureInfo.InvariantCulture) ?? "None");
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
