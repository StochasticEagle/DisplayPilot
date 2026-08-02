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
using DisplayPilot.Display.Interop;
using DisplayPilot.Display.Mccs;
using DisplayPilot.Display.Rotation;
using DisplayPilot.Display.Wmi;
using DisplayPilot.Windows.Scheduling;
using DisplayPilot.Windows.Settings;
using DisplayPilot.Windows.Shell;
using DisplayPilot.Windows.Startup;
using DisplayPilot.Windows.Theme;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Geolocation;
using Windows.Graphics;
using Windows.Storage.Streams;

namespace DisplayPilot.App;

public sealed partial class MainWindow : Window, IDisposable
{
    private const int CompactWidth = 480;
    private const int CompactHeight = 640;
    private const int AdvancedWidth = 900;
    private const int AdvancedHeight = 860;
    private const int BrightnessChangeDelayMilliseconds = 30;
    private const string CompactIconResourceName =
        "DisplayPilot.App.Assets.displaypilot-compact.ico";
    private const string PrimaryImageResourceName =
        "DisplayPilot.App.Assets.displaypilot-primary-256.png";
    private static readonly long CompactReopenDelayMilliseconds =
        NotificationAreaIcon.ActivationGuardDurationMilliseconds;
    private readonly IMonitorDiscoveryService _monitorDiscovery = new DisplayConfigMonitorDiscovery();
    private readonly DdcBrightnessProbeService _ddcProbeService = new();
    private readonly WmiBrightnessProbeService _wmiProbeService = new();
    private readonly BrightnessControlService _brightnessControlService = new();
    private readonly WindowsThemeService _themeService = new();
    private readonly JsonThemeScheduleSettingsStore _themeScheduleSettingsStore = new();
    private readonly WindowsBoundaryTimer _themeScheduleTimer = new();
    private readonly Dictionary<string, int> _compactBrightnessValues =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _compactContrastValues =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MonitorDisplayInfo> _activeMonitors = [];
    private IReadOnlyList<MonitorDdcProbeInfo> _lastDdcProbes = [];
    private IReadOnlyList<WmiBrightnessProbeResult> _lastWmiProbes = [];
    private IReadOnlyList<MonitorDdcVcpFeatureInfo> _lastContrastProbes = [];
    private IReadOnlyList<MonitorDdcVcpFeatureInfo> _lastColorTemperatureProbes = [];
    private IReadOnlyList<MonitorDdcCapabilitiesInfo> _lastDdcCapabilities = [];
    private IReadOnlyList<DisplayRotationResult> _lastRotationReads = [];
    private ThemeState? _themeState;
    private ThemeApplyResult? _lastThemeResult;
    private CustomThemeSchedule? _customThemeSchedule;
    private CustomThemeSchedule? _fixedThemeSchedule;
    private CustomThemeSchedule? _savedThemeSchedule;
    private ThemeScheduleEvaluation? _lastScheduleEvaluation;
    private bool _scheduleWasLoadedFromDisk;
    private bool _scheduleAutomationEnabled;
    private bool _reduceBrightnessOnSchedule;
    private ThemeScheduleMode _scheduleMode = ThemeScheduleMode.FixedTimes;
    private SolarLocation? _solarLocation;
    private SolarTimes? _solarTimes;
    private bool _locationRequestRunning;
    private bool _brightnessReductionActive;
    private readonly Dictionary<string, int> _brightnessRestoreValues =
        new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _manualScheduleOverrideUntil;
    private string? _scheduleSettingsError;
    private BrightnessWriteResult? _lastBrightnessWriteResult;
    private DdcVcpWriteResult? _lastDdcVcpWriteResult;
    private DisplayRotationResult? _lastRotationWriteResult;
    private bool _themeOperationRunning;
    private bool _displayOperationRunning;
    private bool _initialScanStarted;
    private bool _displayScanStarted;
    private bool _sessionIsActive = true;
    private bool _advancedIconLoaded;
    private bool _updatingCompactControls;
    private CancellationTokenSource? _brightnessChangeCancellation;
    private CancellationTokenSource? _contrastChangeCancellation;
    private string? _pendingBrightnessDevicePath;
    private int _pendingBrightnessPercent;
    private string? _pendingContrastDevicePath;
    private int _pendingContrastPercent;
    private bool _isCompactMode = true;
    private long _compactShowBlockedUntil;
    private bool _exitRequested;
    private bool _disposed;
    private NotificationAreaIcon? _notificationAreaIcon;
    private TrayContextMenuWindow? _trayContextMenuWindow;
    private StartupRegistrationStatus _startupRegistrationStatus =
        StartupRegistrationStatus.Unavailable;
    private string? _startupRegistrationError;
    private string _diagnosticReport = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        AdvancedVersionText.Text = $"Version {GetDisplayVersion()}";
        if (Environment.ProcessPath is { } processPath)
        {
            AppWindow.SetIcon(processPath);
        }

        ConfigureCompactWindow();
        _themeScheduleTimer.Elapsed += ThemeScheduleTimer_Elapsed;
        Activated += MainWindow_Activated;
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        LoadScheduleSettings();
        RefreshSchedulePreview();
        RefreshStartupRegistration();
        SystemText.Text = GetSystemSummary();
    }

    private static string GetDisplayVersion()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        return version is null ? "0.5.0" : version.ToString(3);
    }

    public bool InitializeNotificationArea()
    {
        try
        {
            var window = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _notificationAreaIcon = new NotificationAreaIcon(
                window,
                ReadEmbeddedResource(CompactIconResourceName));
            _notificationAreaIcon.PrimaryInvoked += NotificationAreaIcon_PrimaryInvoked;
            _notificationAreaIcon.ContextMenuInvoked += NotificationAreaIcon_ContextMenuInvoked;
            _notificationAreaIcon.AdvancedInvoked += NotificationAreaIcon_AdvancedInvoked;
            _notificationAreaIcon.ExitInvoked += NotificationAreaIcon_ExitInvoked;
            _notificationAreaIcon.SessionActivityChanged +=
                NotificationAreaIcon_SessionActivityChanged;
            return true;
        }
        catch (Win32Exception exception)
        {
            CompactStatusText.Text = $"Notification-area icon unavailable: {exception.Message}";
            return false;
        }
    }

    private static byte[] ReadEmbeddedResource(string resourceName)
    {
        using var resource = typeof(MainWindow).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    private async void LoadAdvancedIcon()
    {
        if (_advancedIconLoaded)
        {
            return;
        }

        _advancedIconLoaded = true;
        var imageBytes = ReadEmbeddedResource(PrimaryImageResourceName);
        using var imageStream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(imageStream))
        {
            writer.WriteBytes(imageBytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        imageStream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(imageStream);
        AdvancedIcon.Source = image;
    }

    public async Task InitializeAsync(bool includeDisplays = true)
    {
        if (!_initialScanStarted)
        {
            _initialScanStarted = true;
            RefreshThemeStatus();
            if (_sessionIsActive)
            {
                await EvaluateAndApplyScheduleAsync();
                UpdateThemeScheduleTimer();
            }
        }

        if (includeDisplays && !_displayScanStarted && _sessionIsActive)
        {
            _displayScanStarted = true;
            await RefreshDisplaysAsync();
            await EvaluateAndApplyScheduleAsync();
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private void NotificationAreaIcon_PrimaryInvoked(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(HandleNotificationAreaPrimaryInvocation);
    }

    private async void HandleNotificationAreaPrimaryInvocation()
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

        await InitializeAsync();
        if (!_sessionIsActive)
        {
            return;
        }

        ShowCompactView();
        RefreshDiagnosticReport();
        if (_activeMonitors.Count > 0)
        {
            await ProbeDdcBrightnessAsync();
            await EvaluateAndApplyScheduleAsync();
        }
    }

    private void NotificationAreaIcon_ContextMenuInvoked(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(ShowNotificationAreaContextMenu);
    }

    private void ShowNotificationAreaContextMenu()
    {
        _trayContextMenuWindow?.Close();

        var menu = new TrayContextMenuWindow();
        menu.AdvancedInvoked += TrayContextMenuWindow_AdvancedInvoked;
        menu.ExitInvoked += TrayContextMenuWindow_ExitInvoked;
        menu.Closed += TrayContextMenuWindow_Closed;
        _trayContextMenuWindow = menu;

        NotificationAreaBounds? anchor = null;
        if (_notificationAreaIcon?.TryGetBounds(out var bounds) == true)
        {
            anchor = bounds;
        }

        menu.ShowAt(anchor);
    }

    private void TrayContextMenuWindow_AdvancedInvoked(object? sender, EventArgs e)
    {
        _notificationAreaIcon?.InvokeContextMenuCommand(
            NotificationAreaMenuCommand.Advanced);
    }

    private void TrayContextMenuWindow_ExitInvoked(object? sender, EventArgs e)
    {
        _notificationAreaIcon?.InvokeContextMenuCommand(
            NotificationAreaMenuCommand.Exit);
    }

    private void TrayContextMenuWindow_Closed(object sender, WindowEventArgs args)
    {
        if (sender is not TrayContextMenuWindow menu)
        {
            return;
        }

        menu.AdvancedInvoked -= TrayContextMenuWindow_AdvancedInvoked;
        menu.ExitInvoked -= TrayContextMenuWindow_ExitInvoked;
        menu.Closed -= TrayContextMenuWindow_Closed;
        if (ReferenceEquals(_trayContextMenuWindow, menu))
        {
            _trayContextMenuWindow = null;
        }
    }

    private void NotificationAreaIcon_AdvancedInvoked(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            await InitializeAsync();
            ShowAdvancedView();
            if (_activeMonitors.Count > 0)
            {
                await ProbeDdcBrightnessAsync();
                await EvaluateAndApplyScheduleAsync();
            }
            RefreshDiagnosticReport();
        });
    }

    private void NotificationAreaIcon_ExitInvoked(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(ExitApplication);
    }

    private void NotificationAreaIcon_SessionActivityChanged(object? sender, bool isActive)
    {
        _ = DispatcherQueue.TryEnqueue(() => SetSessionActivity(isActive));
    }

    private async void SetSessionActivity(bool isActive)
    {
        if (_sessionIsActive == isActive)
        {
            return;
        }

        _sessionIsActive = isActive;
        if (!isActive)
        {
            _brightnessChangeCancellation?.Cancel();
            _contrastChangeCancellation?.Cancel();
            _themeScheduleTimer.Cancel();
            _trayContextMenuWindow?.Close();
            AppWindow.Hide();
            return;
        }

        var displaysWereInitialized = _displayScanStarted;
        await InitializeAsync();
        await EvaluateAndApplyScheduleAsync();
        UpdateThemeScheduleTimer();
        if (displaysWereInitialized)
        {
            await RefreshDisplaysAsync();
            await EvaluateAndApplyScheduleAsync();
        }
    }

    public async void ShowFromExternalActivation()
    {
        if (!_sessionIsActive)
        {
            return;
        }

        await InitializeAsync();
        ShowCompactView();
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
        await EvaluateAndApplyScheduleAsync();
    }

    private async void OpenStartupSettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await global::Windows.System.Launcher.LaunchUriAsync(
                new Uri("ms-settings:startupapps"));
            RefreshStartupRegistration();
        }
        catch (InvalidOperationException exception)
        {
            ReportStartupRegistrationFailure(exception);
        }

        RefreshDiagnosticReport();
    }

    private async void CompactBrightnessSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_updatingCompactControls ||
            sender is not Slider { Tag: CompactMonitorViewModel monitor } ||
            !monitor.IsBrightnessAvailable)
        {
            return;
        }

        var requestedPercent = Math.Clamp((int)Math.Round(e.NewValue), 0, 100);
        if ((_compactBrightnessValues.TryGetValue(monitor.DevicePath, out var currentPercent) &&
             currentPercent == requestedPercent) ||
            (_pendingBrightnessDevicePath is not null &&
             string.Equals(
                 _pendingBrightnessDevicePath,
                 monitor.DevicePath,
                 StringComparison.OrdinalIgnoreCase) &&
             _pendingBrightnessPercent == requestedPercent))
        {
            return;
        }

        _compactBrightnessValues[monitor.DevicePath] = requestedPercent;
        _pendingBrightnessDevicePath = monitor.DevicePath;
        _pendingBrightnessPercent = requestedPercent;
        _brightnessChangeCancellation?.Cancel();
        _brightnessChangeCancellation?.Dispose();
        _contrastChangeCancellation?.Cancel();
        _contrastChangeCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _brightnessChangeCancellation = cancellation;

        try
        {
            await Task.Delay(BrightnessChangeDelayMilliseconds, cancellation.Token);
            while (_displayOperationRunning)
            {
                await Task.Delay(50, cancellation.Token);
            }

            await SetBrightnessAsync(monitor.DevicePath, requestedPercent);
            if (ReferenceEquals(_brightnessChangeCancellation, cancellation))
            {
                _pendingBrightnessDevicePath = null;
                _brightnessChangeCancellation = null;
                cancellation.Dispose();
                UpdateCompactMonitorCards();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer slider value superseded this write.
        }
    }

    private async void CompactContrastSlider_ValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_updatingCompactControls ||
            sender is not Slider { Tag: CompactMonitorViewModel monitor } ||
            !monitor.IsContrastAvailable)
        {
            return;
        }

        var requestedPercent = Math.Clamp((int)Math.Round(e.NewValue), 0, 100);
        if ((_compactContrastValues.TryGetValue(monitor.DevicePath, out var currentPercent) &&
             currentPercent == requestedPercent) ||
            (_pendingContrastDevicePath is not null &&
             string.Equals(_pendingContrastDevicePath, monitor.DevicePath, StringComparison.OrdinalIgnoreCase) &&
             _pendingContrastPercent == requestedPercent))
        {
            return;
        }

        _compactContrastValues[monitor.DevicePath] = requestedPercent;
        _pendingContrastDevicePath = monitor.DevicePath;
        _pendingContrastPercent = requestedPercent;
        _contrastChangeCancellation?.Cancel();
        _contrastChangeCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _contrastChangeCancellation = cancellation;

        try
        {
            await Task.Delay(BrightnessChangeDelayMilliseconds, cancellation.Token);
            while (_displayOperationRunning)
            {
                await Task.Delay(50, cancellation.Token);
            }

            await SetContrastAsync(monitor.DevicePath, requestedPercent);
            if (ReferenceEquals(_contrastChangeCancellation, cancellation))
            {
                _pendingContrastDevicePath = null;
                _contrastChangeCancellation = null;
                cancellation.Dispose();
                UpdateCompactMonitorCards();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer slider value superseded this write.
        }
    }

    private async void CompactColorTemperature_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updatingCompactControls ||
            sender is not ComboBox
            {
                Tag: CompactMonitorViewModel monitor,
                SelectedItem: ColorTemperaturePresetViewModel preset,
            } ||
            !monitor.IsColorTemperatureAvailable)
        {
            return;
        }

        var current = GetSuccessfulFeatureRead(
            _lastColorTemperatureProbes,
            monitor.DevicePath);
        if (current?.CurrentValue == preset.RawValue)
        {
            return;
        }

        while (_displayOperationRunning)
        {
            await Task.Delay(50);
        }

        await SetColorTemperatureAsync(monitor.DevicePath, checked((uint)preset.RawValue));
    }

    private async void CompactApplyRotationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button
            {
                Tag: CompactMonitorViewModel
                {
                    IsRotationAvailable: true,
                    SelectedRotation: { } selectedRotation,
                } monitor,
            })
        {
            return;
        }

        await ApplyRotationAsync(monitor.DevicePath, selectedRotation.Rotation);
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

        CopyCompactScheduleToAdvanced();
        _updatingCompactControls = true;
        try
        {
            ScheduleAutomationToggle.IsOn = CompactScheduleAutomationToggle.IsOn;
        }
        finally
        {
            _updatingCompactControls = false;
        }
        UpdateScheduleOptionsVisibility();
        if (SaveScheduleSettings())
        {
            await EvaluateAndApplyScheduleAsync();
            UpdateThemeScheduleTimer();
        }
        else
        {
            _updatingCompactControls = true;
            try
            {
                ScheduleAutomationToggle.IsOn = _scheduleAutomationEnabled;
                CompactScheduleAutomationToggle.IsOn = _scheduleAutomationEnabled;
            }
            finally
            {
                _updatingCompactControls = false;
            }

            CompactStatusText.Text = "Theme schedule could not be saved. Open Advanced for details.";
        }
    }

    private async void ScheduleAutomationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingCompactControls)
        {
            return;
        }

        _updatingCompactControls = true;
        try
        {
            CompactScheduleAutomationToggle.IsOn = ScheduleAutomationToggle.IsOn;
        }
        finally
        {
            _updatingCompactControls = false;
        }

        UpdateScheduleOptionsVisibility();
        if (SaveScheduleSettings())
        {
            await EvaluateAndApplyScheduleAsync();
            UpdateThemeScheduleTimer();
        }
    }

    private void CompactScheduleModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updatingCompactControls)
        {
            return;
        }

        _updatingCompactControls = true;
        try
        {
            ScheduleModeComboBox.SelectedIndex = CompactScheduleModeComboBox.SelectedIndex;
        }
        finally
        {
            _updatingCompactControls = false;
        }

        UpdateScheduleModeVisibility();
    }

    private void ScheduleModeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updatingCompactControls)
        {
            return;
        }

        _updatingCompactControls = true;
        try
        {
            CompactScheduleModeComboBox.SelectedIndex = ScheduleModeComboBox.SelectedIndex;
        }
        finally
        {
            _updatingCompactControls = false;
        }

        UpdateScheduleModeVisibility();
    }

    private async void UseWindowsLocationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_locationRequestRunning)
        {
            return;
        }

        _locationRequestRunning = true;
        UseWindowsLocationButton.IsEnabled = false;
        CompactUseWindowsLocationButton.IsEnabled = false;
        SolarLocationStatusText.Text = "Requesting a one-time location from Windows...";
        CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                SolarLocationStatusText.Text = "Windows location access was not granted. Enter coordinates manually.";
                CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
                return;
            }

            var geolocator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
            };
            var position = await geolocator.GetGeopositionAsync(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromSeconds(15));
            var point = position.Coordinate.Point.Position;
            SetSolarLocationInputs(point.Latitude, point.Longitude, "Windows location");
            RefreshSchedulePreview();
            SolarLocationStatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Windows location acquired (accuracy approximately {0:F0} m).",
                position.Coordinate.Accuracy);
            CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
        }
        catch (UnauthorizedAccessException)
        {
            SolarLocationStatusText.Text = "Windows location is disabled or denied. Enter coordinates manually.";
            CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
        }
        catch (COMException exception)
        {
            SolarLocationStatusText.Text = $"Windows location is unavailable (0x{exception.HResult:X8}).";
            CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
        }
        catch (TimeoutException)
        {
            SolarLocationStatusText.Text = "Windows did not return a location within 15 seconds. Try again or enter coordinates manually.";
            CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
        }
        catch (OperationCanceledException)
        {
            SolarLocationStatusText.Text = "Windows did not return a location. Try again or enter coordinates manually.";
            CompactSolarLocationStatusText.Text = SolarLocationStatusText.Text;
        }
        finally
        {
            _locationRequestRunning = false;
            UseWindowsLocationButton.IsEnabled = true;
            CompactUseWindowsLocationButton.IsEnabled = true;
        }
    }

    private async void SaveCompactScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCompactScheduleToAdvanced();
        _updatingCompactControls = true;
        try
        {
            ScheduleAutomationToggle.IsOn = CompactScheduleAutomationToggle.IsOn;
        }
        finally
        {
            _updatingCompactControls = false;
        }
        if (SaveScheduleSettings())
        {
            await EvaluateAndApplyScheduleAsync();
            UpdateThemeScheduleTimer();
            CompactStatusText.Text = "Theme schedule saved.";
        }
        else
        {
            CompactStatusText.Text = "Theme schedule could not be saved. Open Advanced for details.";
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
        await EvaluateAndApplyScheduleAsync();
    }

    private async void ProbeDdcButton_Click(object sender, RoutedEventArgs e)
    {
        await ProbeDdcBrightnessAsync();
        await EvaluateAndApplyScheduleAsync();
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiagnosticReport();
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
        if (_displayOperationRunning || !_sessionIsActive)
        {
            return;
        }

        _displayOperationRunning = true;
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        CompactReadBrightnessButton.IsEnabled = false;
        CompactMonitorList.IsEnabled = false;
        MonitorList.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Scanning active Windows display paths...";
        CompactStatusText.Text = "Scanning active Windows display paths...";
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            var monitors = await Task.Run(_monitorDiscovery.DiscoverActiveMonitors);

            _activeMonitors = monitors;
            _lastRotationReads = await Task.Run(() => monitors
                .Select(monitor => WindowsDisplayRotationService.Read(monitor.GdiDeviceName))
                .ToArray());
            _lastDdcProbes = [];
            _lastWmiProbes = [];
            _lastContrastProbes = [];
            _lastColorTemperatureProbes = [];
            _lastDdcCapabilities = [];
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
            _lastContrastProbes = [];
            _lastColorTemperatureProbes = [];
            _lastDdcCapabilities = [];
            _lastRotationReads = [];
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
            MonitorList.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task ProbeDdcBrightnessAsync()
    {
        if (_displayOperationRunning || !_sessionIsActive)
        {
            return;
        }

        _displayOperationRunning = true;
        RescanButton.IsEnabled = false;
        ProbeDdcButton.IsEnabled = false;
        CompactReadBrightnessButton.IsEnabled = false;
        CompactMonitorList.IsEnabled = false;
        MonitorList.IsEnabled = false;
        CopyReportButton.IsEnabled = false;
        CopyReportButton.Content = "Copy diagnostic report";
        StatusText.Text = "Reading external DDC/CI and internal WMI brightness...";
        CompactStatusText.Text = "Reading monitor brightness...";

        try
        {
            var probes = await Task.Run(() => (
                Ddc: _ddcProbeService.ProbeBrightness(_activeMonitors),
                Wmi: _wmiProbeService.ProbeBrightness(_activeMonitors),
                Contrast: WindowsDdcVcpFeatureService.ReadFeature(
                    _activeMonitors,
                    NativeConstants.VcpCodeContrast),
                ColorTemperature: WindowsDdcVcpFeatureService.ReadFeature(
                    _activeMonitors,
                    NativeConstants.VcpCodeSelectColorPreset),
                Capabilities: _lastDdcCapabilities.Count == 0
                    ? WindowsDdcVcpFeatureService.ReadCapabilities(_activeMonitors)
                    : _lastDdcCapabilities));
            _lastDdcProbes = probes.Ddc;
            _lastWmiProbes = probes.Wmi;
            _lastContrastProbes = probes.Contrast;
            _lastColorTemperatureProbes = probes.ColorTemperature;
            _lastDdcCapabilities = probes.Capabilities;
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
                : string.Empty;
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
            CompactReadBrightnessButton.IsEnabled = _activeMonitors.Count > 0;
            CompactMonitorList.IsEnabled = true;
            MonitorList.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }
    }

    private async Task<bool> SetBrightnessAsync(string devicePath, int requestedPercent)
    {
        if (_displayOperationRunning || !_sessionIsActive)
        {
            return false;
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
        CompactReadBrightnessButton.IsEnabled = false;
        CompactMonitorList.IsEnabled = false;
        MonitorList.IsEnabled = false;
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
            CompactReadBrightnessButton.IsEnabled = _activeMonitors.Count > 0;
            CompactMonitorList.IsEnabled = true;
            MonitorList.IsEnabled = true;
            CopyReportButton.IsEnabled = true;
        }

        return writeResult?.Succeeded == true;
    }

    private async Task SetContrastAsync(string devicePath, int requestedPercent)
    {
        if (_displayOperationRunning || !_sessionIsActive)
        {
            return;
        }

        var display = FindActiveDisplay(devicePath);
        _displayOperationRunning = true;
        SetDisplayControlsEnabled(false);
        CompactStatusText.Text = $"Setting {display.FriendlyName} contrast to {requestedPercent}%...";
        StatusText.Text = CompactStatusText.Text;

        try
        {
            var result = await Task.Run(() => WindowsDdcVcpFeatureService.WriteContinuousPercent(
                display,
                NativeConstants.VcpCodeContrast,
                requestedPercent));
            _lastDdcVcpWriteResult = result;
            _lastContrastProbes = await Task.Run(() => WindowsDdcVcpFeatureService.ReadFeature(
                _activeMonitors,
                NativeConstants.VcpCodeContrast));
            UpdateCompactMonitorCards();
            CompactStatusText.Text = result.Succeeded
                ? $"{display.FriendlyName}: contrast verified at {result.VerifiedPercent}%."
                : $"Contrast did not verify (error 0x{unchecked((uint)result.ErrorCode):X8}).";
            StatusText.Text = CompactStatusText.Text;
            RefreshDiagnosticReport();
        }
        finally
        {
            _displayOperationRunning = false;
            SetDisplayControlsEnabled(true);
        }
    }

    private async Task SetColorTemperatureAsync(string devicePath, uint requestedValue)
    {
        if (_displayOperationRunning || !_sessionIsActive)
        {
            return;
        }

        var display = FindActiveDisplay(devicePath);
        _displayOperationRunning = true;
        SetDisplayControlsEnabled(false);
        var presetName = VcpNames.GetFormattedValueName(
            NativeConstants.VcpCodeSelectColorPreset,
            checked((int)requestedValue));
        CompactStatusText.Text = $"Setting {display.FriendlyName} color temperature to {presetName}...";
        StatusText.Text = CompactStatusText.Text;

        try
        {
            var result = await Task.Run(() => WindowsDdcVcpFeatureService.WriteDiscreteValue(
                display,
                NativeConstants.VcpCodeSelectColorPreset,
                requestedValue));
            _lastDdcVcpWriteResult = result;
            _lastColorTemperatureProbes = await Task.Run(() => WindowsDdcVcpFeatureService.ReadFeature(
                _activeMonitors,
                NativeConstants.VcpCodeSelectColorPreset));
            UpdateCompactMonitorCards();
            CompactStatusText.Text = result.Succeeded
                ? $"{display.FriendlyName}: color temperature verified as {presetName}."
                : $"Color temperature did not verify (error 0x{unchecked((uint)result.ErrorCode):X8}).";
            StatusText.Text = CompactStatusText.Text;
            RefreshDiagnosticReport();
        }
        finally
        {
            _displayOperationRunning = false;
            SetDisplayControlsEnabled(true);
        }
    }

    private async Task ApplyRotationAsync(
        string devicePath,
        DisplayRotation requestedRotation)
    {
        if (_displayOperationRunning || !_sessionIsActive)
        {
            return;
        }

        var display = FindActiveDisplay(devicePath);
        _displayOperationRunning = true;
        SetDisplayControlsEnabled(false);
        CompactStatusText.Text = $"Rotating {display.FriendlyName} to {FormatRotation(requestedRotation)}...";
        StatusText.Text = CompactStatusText.Text;

        try
        {
            var result = await Task.Run(() => WindowsDisplayRotationService.Apply(
                display.GdiDeviceName,
                requestedRotation));
            _lastRotationWriteResult = result;
            _lastRotationReads = await Task.Run(() => _activeMonitors
                .Select(monitor => WindowsDisplayRotationService.Read(monitor.GdiDeviceName))
                .ToArray());
            UpdateCompactMonitorCards();
            CompactStatusText.Text = result.Status switch
            {
                DisplayRotationStatus.Applied =>
                    $"{display.FriendlyName}: rotation verified as {FormatRotation(requestedRotation)}.",
                DisplayRotationStatus.RestartRequired =>
                    $"{display.FriendlyName}: rotation saved; Windows requires a restart.",
                _ =>
                    $"Rotation failed ({result.Status}, native result {result.NativeResult}).",
            };
            StatusText.Text = CompactStatusText.Text;
            RefreshDiagnosticReport();
        }
        finally
        {
            _displayOperationRunning = false;
            SetDisplayControlsEnabled(true);
        }
    }

    private static string FormatRotation(DisplayRotation rotation) => rotation switch
    {
        DisplayRotation.Landscape => "Landscape (0°)",
        DisplayRotation.Portrait => "Portrait (90°)",
        DisplayRotation.LandscapeFlipped => "Landscape flipped (180°)",
        DisplayRotation.PortraitFlipped => "Portrait flipped (270°)",
        _ => rotation.ToString(),
    };

    private MonitorDisplayInfo FindActiveDisplay(string devicePath) =>
        _activeMonitors.First(candidate => string.Equals(
            candidate.DevicePath,
            devicePath,
            StringComparison.OrdinalIgnoreCase));

    private void SetDisplayControlsEnabled(bool enabled)
    {
        RescanButton.IsEnabled = enabled;
        ProbeDdcButton.IsEnabled = enabled && _activeMonitors.Count > 0;
        CompactReadBrightnessButton.IsEnabled = enabled && _activeMonitors.Count > 0;
        CompactMonitorList.IsEnabled = enabled;
        MonitorList.IsEnabled = enabled;
        CopyReportButton.IsEnabled = enabled;
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
            var mode = GetSelectedScheduleMode();
            if (mode == ThemeScheduleMode.SunriseSunset)
            {
                var location = ReadSolarLocationInputs();
                var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.Local);
                var solar = LocalSolarCalculator.Calculate(
                    DateOnly.FromDateTime(localNow.DateTime),
                    location,
                    TimeZoneInfo.Local);
                UpdateSolarTimeDisplays(solar);
                if (solar.Condition != SolarDayCondition.Normal)
                {
                    _customThemeSchedule = null;
                    _lastScheduleEvaluation = null;
                    ScheduleStatusText.Text = solar.Condition == SolarDayCondition.PolarDay
                        ? "The sun does not set today at this location; Light mode remains active and tomorrow is recalculated at midnight."
                        : "The sun does not rise today at this location; Dark mode remains active and tomorrow is recalculated at midnight.";
                    CompactScheduleStatusText.Text = ScheduleStatusText.Text;
                    RefreshDiagnosticReport();
                    return;
                }

                _customThemeSchedule = new CustomThemeSchedule(
                    TimeOnly.FromDateTime(solar.Sunrise!.Value.DateTime),
                    TimeOnly.FromDateTime(solar.Sunset!.Value.DateTime));
            }
            else
            {
                UpdateSolarTimeDisplays(null);
                _customThemeSchedule = new CustomThemeSchedule(
                    TimeOnly.FromTimeSpan(LightScheduleTimePicker.Time),
                    TimeOnly.FromTimeSpan(DarkScheduleTimePicker.Time));
            }

            _lastScheduleEvaluation = CustomThemeScheduleEvaluator.Evaluate(
                _customThemeSchedule,
                TimeOnly.FromDateTime(DateTime.Now));

            var remainingMinutes = (int)Math.Ceiling(_lastScheduleEvaluation.TimeUntilNextTransition.TotalMinutes);
            var automationStatus = _scheduleAutomationEnabled
                ? "Automatic switching is enabled while DisplayPilot is running."
                : "Preview only; automatic switching is disabled.";
            ScheduleStatusText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "{0}: Light {1} · Dark {2}. Now: {3}. Next: {4} at {5} ({6} minute(s)). {7}",
                mode == ThemeScheduleMode.SunriseSunset ? "Solar schedule" : "Fixed schedule",
                _customThemeSchedule.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                _customThemeSchedule.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                _lastScheduleEvaluation.ActiveMode,
                _lastScheduleEvaluation.NextMode,
                FormatTime(_lastScheduleEvaluation.NextTransitionTime),
                remainingMinutes,
                automationStatus);
            CompactScheduleStatusText.Text = ScheduleStatusText.Text;
            RefreshDiagnosticReport();
        }
        catch (ArgumentException exception)
        {
            UpdateSolarTimeDisplays(null);
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
            CopyAdvancedScheduleToCompact();
            _fixedThemeSchedule = result.Schedule;
            _scheduleMode = result.ScheduleMode;
            _solarLocation = result.SolarLocation;
            RefreshEffectiveSchedule(DateTimeOffset.Now);
            _scheduleWasLoadedFromDisk = result.WasLoadedFromDisk;
            _scheduleAutomationEnabled = result.AutomationEnabled;
            _reduceBrightnessOnSchedule = result.ReduceBrightness;
            _brightnessReductionActive = result.BrightnessReductionActive;
            _brightnessRestoreValues.Clear();
            foreach (var pair in result.BrightnessRestoreValues)
            {
                _brightnessRestoreValues[pair.Key] = pair.Value;
            }
            _updatingCompactControls = true;
            try
            {
                ScheduleAutomationToggle.IsOn = result.AutomationEnabled;
                CompactScheduleAutomationToggle.IsOn = result.AutomationEnabled;
                ReduceBrightnessCheckBox.IsChecked = result.ReduceBrightness;
                CompactReduceBrightnessCheckBox.IsChecked = result.ReduceBrightness;
                ScheduleModeComboBox.SelectedIndex = (int)result.ScheduleMode;
                CompactScheduleModeComboBox.SelectedIndex = (int)result.ScheduleMode;
            }
            finally
            {
                _updatingCompactControls = false;
            }

            if (result.SolarLocation is not null)
            {
                SetSolarLocationInputs(
                    result.SolarLocation.Latitude,
                    result.SolarLocation.Longitude,
                    result.SolarLocation.Label);
            }

            _scheduleSettingsError = null;
            SchedulePersistenceStatusText.Text = result.WasLoadedFromDisk
                ? result.AutomationEnabled
                    ? "Loaded the saved schedule; automatic switching is enabled while the app runs."
                    : "Loaded the saved per-user schedule; automatic switching is disabled."
                : "Using the default schedule; select Save schedule to persist it.";
            UpdateCompactScheduleStatus();
            UpdateScheduleOptionsVisibility();
            UpdateScheduleModeVisibility();
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
        CopyAdvancedScheduleToCompact();
        _fixedThemeSchedule = defaults;
        _savedThemeSchedule = defaults;
        _scheduleMode = ThemeScheduleMode.FixedTimes;
        _solarLocation = null;
        _solarTimes = null;
        _scheduleWasLoadedFromDisk = false;
        _scheduleAutomationEnabled = false;
        _reduceBrightnessOnSchedule = false;
        _brightnessReductionActive = false;
        _brightnessRestoreValues.Clear();
        _updatingCompactControls = true;
        try
        {
            ScheduleAutomationToggle.IsOn = false;
            CompactScheduleAutomationToggle.IsOn = false;
            ReduceBrightnessCheckBox.IsChecked = false;
            CompactReduceBrightnessCheckBox.IsChecked = false;
            ScheduleModeComboBox.SelectedIndex = (int)ThemeScheduleMode.FixedTimes;
            CompactScheduleModeComboBox.SelectedIndex = (int)ThemeScheduleMode.FixedTimes;
        }
        finally
        {
            _updatingCompactControls = false;
        }

        _scheduleSettingsError = exception.GetType().Name;
        SchedulePersistenceStatusText.Text = "Saved schedule could not be loaded; using safe defaults.";
        UpdateCompactScheduleStatus();
        UpdateScheduleOptionsVisibility();
        UpdateScheduleModeVisibility();
    }

    private bool SaveScheduleSettings()
    {
        try
        {
            var schedule = new CustomThemeSchedule(
                TimeOnly.FromTimeSpan(LightScheduleTimePicker.Time),
                TimeOnly.FromTimeSpan(DarkScheduleTimePicker.Time));
            var automationEnabled = ScheduleAutomationToggle.IsOn;
            var reduceBrightness = ReduceBrightnessCheckBox.IsChecked == true;
            var scheduleMode = GetSelectedScheduleMode();
            var solarLocation = scheduleMode == ThemeScheduleMode.SunriseSunset
                ? ReadSolarLocationInputs()
                : null;
            _themeScheduleSettingsStore.Save(
                schedule,
                automationEnabled,
                reduceBrightness,
                _brightnessReductionActive,
                _brightnessRestoreValues,
                scheduleMode,
                solarLocation);
            _fixedThemeSchedule = schedule;
            _scheduleMode = scheduleMode;
            _solarLocation = solarLocation;
            RefreshEffectiveSchedule(DateTimeOffset.Now);
            _scheduleAutomationEnabled = automationEnabled;
            _reduceBrightnessOnSchedule = reduceBrightness;
            _updatingCompactControls = true;
            try
            {
                CompactScheduleAutomationToggle.IsOn = automationEnabled;
                CompactReduceBrightnessCheckBox.IsChecked = reduceBrightness;
            }
            finally
            {
                _updatingCompactControls = false;
            }

            _scheduleWasLoadedFromDisk = true;
            _manualScheduleOverrideUntil = null;
            _scheduleSettingsError = null;
            CopyAdvancedScheduleToCompact();
            SchedulePersistenceStatusText.Text = _scheduleAutomationEnabled
                ? "Saved the schedule; automatic switching is enabled while the app runs."
                : "Saved the schedule; automatic switching is disabled.";
            RefreshSchedulePreview();
            UpdateCompactScheduleStatus();
            UpdateScheduleOptionsVisibility();
            UpdateScheduleModeVisibility();
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

    private void CopyCompactScheduleToAdvanced()
    {
        LightScheduleTimePicker.Time = CompactLightScheduleTimePicker.Time;
        DarkScheduleTimePicker.Time = CompactDarkScheduleTimePicker.Time;
        ReduceBrightnessCheckBox.IsChecked = CompactReduceBrightnessCheckBox.IsChecked;
        ScheduleModeComboBox.SelectedIndex = CompactScheduleModeComboBox.SelectedIndex;
        SolarLatitudeNumberBox.Value = CompactSolarLatitudeNumberBox.Value;
        SolarLongitudeNumberBox.Value = CompactSolarLongitudeNumberBox.Value;
        SolarLocationLabelTextBox.Text = CompactSolarLocationLabelTextBox.Text;
    }

    private void CopyAdvancedScheduleToCompact()
    {
        CompactLightScheduleTimePicker.Time = LightScheduleTimePicker.Time;
        CompactDarkScheduleTimePicker.Time = DarkScheduleTimePicker.Time;
        CompactReduceBrightnessCheckBox.IsChecked = ReduceBrightnessCheckBox.IsChecked;
        CompactScheduleModeComboBox.SelectedIndex = ScheduleModeComboBox.SelectedIndex;
        CompactSolarLatitudeNumberBox.Value = SolarLatitudeNumberBox.Value;
        CompactSolarLongitudeNumberBox.Value = SolarLongitudeNumberBox.Value;
        CompactSolarLocationLabelTextBox.Text = SolarLocationLabelTextBox.Text;
    }

    private void SaveBrightnessScheduleState()
    {
        if (_fixedThemeSchedule is null)
        {
            return;
        }

        _themeScheduleSettingsStore.Save(
            _fixedThemeSchedule,
            _scheduleAutomationEnabled,
            _reduceBrightnessOnSchedule,
            _brightnessReductionActive,
            _brightnessRestoreValues,
            _scheduleMode,
            _solarLocation);
    }

    private void UpdateScheduleOptionsVisibility()
    {
        var visibility = ScheduleAutomationToggle.IsOn
            ? Visibility.Visible
            : Visibility.Collapsed;
        ScheduleOptions.Visibility = visibility;
        CompactScheduleOptions.Visibility = visibility;
        ScheduleStatusText.Visibility = visibility;
        CompactScheduleStatusText.Visibility = visibility;
        var brightnessOptionVisibility = visibility == Visibility.Visible &&
            _activeMonitors.Any(display => HasValidatedWritePath(display.DevicePath))
                ? Visibility.Visible
                : Visibility.Collapsed;
        ReduceBrightnessCheckBox.Visibility = brightnessOptionVisibility;
        CompactReduceBrightnessCheckBox.Visibility = brightnessOptionVisibility;
    }

    private void UpdateScheduleModeVisibility()
    {
        var solarMode = GetSelectedScheduleMode() == ThemeScheduleMode.SunriseSunset;
        FixedScheduleOptions.Visibility = solarMode ? Visibility.Collapsed : Visibility.Visible;
        CompactFixedScheduleOptions.Visibility = solarMode ? Visibility.Collapsed : Visibility.Visible;
        SolarScheduleOptions.Visibility = solarMode ? Visibility.Visible : Visibility.Collapsed;
        CompactSolarScheduleOptions.Visibility = solarMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private ThemeScheduleMode GetSelectedScheduleMode() =>
        ScheduleModeComboBox.SelectedIndex == (int)ThemeScheduleMode.SunriseSunset
            ? ThemeScheduleMode.SunriseSunset
            : ThemeScheduleMode.FixedTimes;

    private SolarLocation ReadSolarLocationInputs()
    {
        if (double.IsNaN(SolarLatitudeNumberBox.Value) ||
            double.IsNaN(SolarLongitudeNumberBox.Value))
        {
            throw new ArgumentException("Latitude and longitude are required for sunrise/sunset scheduling.");
        }

        return new SolarLocation(
            SolarLatitudeNumberBox.Value,
            SolarLongitudeNumberBox.Value,
            SolarLocationLabelTextBox.Text);
    }

    private void SetSolarLocationInputs(double latitude, double longitude, string? label)
    {
        _updatingCompactControls = true;
        try
        {
            SolarLatitudeNumberBox.Value = latitude;
            SolarLongitudeNumberBox.Value = longitude;
            SolarLocationLabelTextBox.Text = label ?? string.Empty;
            CompactSolarLatitudeNumberBox.Value = latitude;
            CompactSolarLongitudeNumberBox.Value = longitude;
            CompactSolarLocationLabelTextBox.Text = label ?? string.Empty;
        }
        finally
        {
            _updatingCompactControls = false;
        }
    }

    private void RefreshEffectiveSchedule(DateTimeOffset now)
    {
        if (_scheduleMode == ThemeScheduleMode.FixedTimes || _solarLocation is null)
        {
            _solarTimes = null;
            UpdateSolarTimeDisplays(null);
            _savedThemeSchedule = _fixedThemeSchedule;
            return;
        }

        var localNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local);
        _solarTimes = LocalSolarCalculator.Calculate(
            DateOnly.FromDateTime(localNow.DateTime),
            _solarLocation,
            TimeZoneInfo.Local);
        UpdateSolarTimeDisplays(_solarTimes);
        _savedThemeSchedule = _solarTimes.Condition == SolarDayCondition.Normal
            ? new CustomThemeSchedule(
                TimeOnly.FromDateTime(_solarTimes.Sunrise!.Value.DateTime),
                TimeOnly.FromDateTime(_solarTimes.Sunset!.Value.DateTime))
            : null;
    }

    private void UpdateSolarTimeDisplays(SolarTimes? solarTimes)
    {
        var hasTimes = solarTimes is
        {
            Condition: SolarDayCondition.Normal,
            Sunrise: not null,
            Sunset: not null,
        };
        SolarCalculatedTimes.Visibility = hasTimes ? Visibility.Visible : Visibility.Collapsed;
        CompactSolarCalculatedTimes.Visibility = hasTimes ? Visibility.Visible : Visibility.Collapsed;
        if (!hasTimes)
        {
            return;
        }

        var lightTime = solarTimes!.Sunrise!.Value.TimeOfDay;
        var darkTime = solarTimes.Sunset!.Value.TimeOfDay;
        SolarLightTimePicker.Time = lightTime;
        SolarDarkTimePicker.Time = darkTime;
        CompactSolarLightTimePicker.Time = lightTime;
        CompactSolarDarkTimePicker.Time = darkTime;
    }

    private ThemeScheduleEvaluation? EvaluateSavedSchedule(DateTimeOffset now)
    {
        if (_scheduleMode == ThemeScheduleMode.SunriseSunset)
        {
            RefreshEffectiveSchedule(now);

            if (_solarTimes?.Condition is SolarDayCondition.PolarDay or SolarDayCondition.PolarNight)
            {
                var activeMode = _solarTimes.Condition == SolarDayCondition.PolarDay
                    ? ThemeMode.Light
                    : ThemeMode.Dark;
                var localNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local);
                var nextMidnightLocal = localNow.Date.AddDays(1);
                var nextMidnight = new DateTimeOffset(
                    nextMidnightLocal,
                    TimeZoneInfo.Local.GetUtcOffset(nextMidnightLocal));
                return new ThemeScheduleEvaluation(
                    activeMode,
                    TimeOnly.MinValue,
                    activeMode,
                    nextMidnight - now);
            }
        }

        return _savedThemeSchedule is null
            ? null
            : CustomThemeScheduleEvaluator.Evaluate(
                _savedThemeSchedule,
                TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime));
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
        _brightnessChangeCancellation?.Cancel();
        _brightnessChangeCancellation?.Dispose();
        Activated -= MainWindow_Activated;
        AppWindow.Closing -= AppWindow_Closing;
        Closed -= MainWindow_Closed;
        _themeScheduleTimer.Elapsed -= ThemeScheduleTimer_Elapsed;
        if (_notificationAreaIcon is not null)
        {
            _notificationAreaIcon.PrimaryInvoked -= NotificationAreaIcon_PrimaryInvoked;
            _notificationAreaIcon.ContextMenuInvoked -= NotificationAreaIcon_ContextMenuInvoked;
            _notificationAreaIcon.AdvancedInvoked -= NotificationAreaIcon_AdvancedInvoked;
            _notificationAreaIcon.ExitInvoked -= NotificationAreaIcon_ExitInvoked;
            _notificationAreaIcon.SessionActivityChanged -=
                NotificationAreaIcon_SessionActivityChanged;
            _notificationAreaIcon.Dispose();
        }

        _trayContextMenuWindow?.Close();
        _trayContextMenuWindow = null;
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
        LoadAdvancedIcon();
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

    private async Task EvaluateAndApplyScheduleAsync()
    {
        if (!_sessionIsActive || _themeOperationRunning)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var scheduledEvaluation = EvaluateSavedSchedule(now);
        if (scheduledEvaluation is null)
        {
            return;
        }

        await ReconcileScheduledBrightnessAsync(scheduledEvaluation.ActiveMode);

        if (!_scheduleAutomationEnabled)
        {
            return;
        }

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

    private async Task ReconcileScheduledBrightnessAsync(ThemeMode scheduledMode)
    {
        var shouldReduce = _scheduleAutomationEnabled &&
            _reduceBrightnessOnSchedule &&
            scheduledMode == ThemeMode.Dark;
        if ((!shouldReduce && !_brightnessReductionActive) || _displayOperationRunning)
        {
            return;
        }

        if (_activeMonitors.Count == 0)
        {
            return;
        }

        var hasUnreducedDisplay = shouldReduce && _activeMonitors.Any(display =>
            !_brightnessRestoreValues.ContainsKey(display.DevicePath));
        if (hasUnreducedDisplay ||
            _lastDdcProbes.Count == 0 ||
            _lastWmiProbes.Count == 0)
        {
            await ProbeDdcBrightnessAsync();
        }

        if (shouldReduce)
        {
            await ReduceScheduledBrightnessAsync();
        }
        else
        {
            await RestoreScheduledBrightnessAsync();
        }
    }

    private async Task ReduceScheduledBrightnessAsync()
    {
        foreach (var display in _activeMonitors)
        {
            if (_brightnessRestoreValues.ContainsKey(display.DevicePath))
            {
                continue;
            }

            if (!TryGetCurrentBrightnessPercent(display.DevicePath, out var currentPercent))
            {
                continue;
            }

            var reducedPercent = ScheduledBrightness.CalculateReducedValue(currentPercent);
            _brightnessRestoreValues[display.DevicePath] = currentPercent;
            _brightnessReductionActive = true;
            if (!TrySaveBrightnessScheduleState())
            {
                _brightnessRestoreValues.Remove(display.DevicePath);
                _brightnessReductionActive = _brightnessRestoreValues.Count > 0;
                continue;
            }

            if (!await SetBrightnessAsync(display.DevicePath, reducedPercent))
            {
                // Keep the original value: the write may have reached the monitor
                // even when verification failed, so restoration must remain possible.
                continue;
            }
        }
    }

    private async Task RestoreScheduledBrightnessAsync()
    {
        foreach (var pair in _brightnessRestoreValues.ToArray())
        {
            if (!_activeMonitors.Any(display => string.Equals(
                    display.DevicePath,
                    pair.Key,
                    StringComparison.OrdinalIgnoreCase)) ||
                !HasValidatedWritePath(pair.Key))
            {
                continue;
            }

            if (!await SetBrightnessAsync(pair.Key, pair.Value))
            {
                continue;
            }

            _brightnessRestoreValues.Remove(pair.Key);
            _brightnessReductionActive = _brightnessRestoreValues.Count > 0;
            TrySaveBrightnessScheduleState();
        }

        _brightnessReductionActive = _brightnessRestoreValues.Count > 0;
        TrySaveBrightnessScheduleState();
    }

    private bool TryGetCurrentBrightnessPercent(string devicePath, out int percent)
    {
        var wmiProbe = _lastWmiProbes.FirstOrDefault(probe =>
            string.Equals(probe.Display.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase) &&
            probe.Status == WmiBrightnessProbeStatus.ReadSucceeded);
        if (wmiProbe is not null)
        {
            percent = wmiProbe.CurrentBrightness;
            return true;
        }

        var ddcRead = _lastDdcProbes
            .FirstOrDefault(probe => string.Equals(
                probe.Display.DevicePath,
                devicePath,
                StringComparison.OrdinalIgnoreCase))?
            .PhysicalMonitors.FirstOrDefault(result =>
                result.Status == DdcBrightnessProbeStatus.ReadSucceeded &&
                result.MaximumValue > 0);
        if (ddcRead is not null)
        {
            percent = Math.Clamp(
                (int)Math.Round(ddcRead.CurrentValue * 100d / ddcRead.MaximumValue),
                0,
                100);
            return true;
        }

        percent = 0;
        return false;
    }

    private bool TrySaveBrightnessScheduleState()
    {
        try
        {
            SaveBrightnessScheduleState();
            _scheduleSettingsError = null;
            return true;
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

    private void ActivateManualScheduleOverride(ThemeMode appliedMode)
    {
        if (!_scheduleAutomationEnabled)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var scheduledEvaluation = EvaluateSavedSchedule(now);
        if (scheduledEvaluation is null)
        {
            _manualScheduleOverrideUntil = null;
            return;
        }
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
        if (!_sessionIsActive ||
            !_scheduleAutomationEnabled ||
            !_initialScanStarted)
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
        var evaluation = EvaluateSavedSchedule(now);
        if (evaluation is null)
        {
            _themeScheduleTimer.Cancel();
            return;
        }

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

    private void RefreshStartupRegistration()
    {
        try
        {
            var registration = WindowsStartupService.ReadRegistration();
            _startupRegistrationStatus = registration.Status;
            _startupRegistrationError = null;
            StartupStatusText.Text = registration.Status switch
            {
                StartupRegistrationStatus.PerUserRegistered =>
                    "DisplayPilot is registered at sign-in for this account. Use Windows Startup settings to enable or disable it independently.",
                StartupRegistrationStatus.Disabled =>
                    "DisplayPilot is not registered for automatic startup in this account. Reinstall it with the all-users startup option selected.",
                _ => "This account's startup registration is unavailable.",
            };
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportStartupRegistrationFailure(exception);
        }
        catch (SecurityException exception)
        {
            ReportStartupRegistrationFailure(exception);
        }
        catch (IOException exception)
        {
            ReportStartupRegistrationFailure(exception);
        }
    }

    private void ReportStartupRegistrationFailure(Exception exception)
    {
        _startupRegistrationStatus = StartupRegistrationStatus.Unavailable;
        _startupRegistrationError = exception.GetType().Name;
        StartupStatusText.Text =
            $"Startup registration could not be read: {exception.Message}";
    }

    private void SetThemeButtonsEnabled(bool enabled)
    {
        RefreshThemeButton.IsEnabled = enabled;
        ApplyLightThemeButton.IsEnabled = enabled;
        ApplyDarkThemeButton.IsEnabled = enabled;
        CompactDarkModeToggle.IsEnabled = enabled;
    }

    private void UpdateCompactMonitorCards()
    {
        var cards = _activeMonitors.Select(display =>
        {
            var capabilities = _lastDdcCapabilities.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                display.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            var contrastRead = GetSuccessfulFeatureRead(_lastContrastProbes, display.DevicePath);
            var contrastAvailable =
                capabilities?.Capabilities.SupportsVcpCode(NativeConstants.VcpCodeContrast) == true &&
                contrastRead is { MaximumValue: > 0 };
            var contrastPercent = contrastRead is { MaximumValue: > 0 }
                ? Math.Clamp(contrastRead.CurrentValue * 100d / contrastRead.MaximumValue, 0, 100)
                : 0;
            var colorTemperatureRead = GetSuccessfulFeatureRead(
                _lastColorTemperatureProbes,
                display.DevicePath);
            var colorTemperaturePresets = GetColorTemperaturePresets(display.DevicePath);
            var rotationRead = _lastRotationReads.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.GdiDeviceName,
                    display.GdiDeviceName,
                    StringComparison.OrdinalIgnoreCase));
            var rotationAvailable = rotationRead?.Status == DisplayRotationStatus.ReadSucceeded;

            var wmiProbe = _lastWmiProbes.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                display.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            if (wmiProbe?.Status == WmiBrightnessProbeStatus.ReadSucceeded)
            {
                return new CompactMonitorViewModel(
                    display.DevicePath,
                    display.GdiDeviceName,
                    display.FriendlyName,
                    isBrightnessAvailable: true,
                    GetCompactBrightness(display.DevicePath, wmiProbe.CurrentBrightness),
                    isContrastAvailable: contrastAvailable,
                    GetCompactContrast(display.DevicePath, contrastPercent),
                    colorTemperaturePresets,
                    colorTemperatureRead is null ? null : checked((int)colorTemperatureRead.CurrentValue),
                    rotationAvailable,
                    rotationRead?.Rotation,
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
                    display.GdiDeviceName,
                    display.FriendlyName,
                    isBrightnessAvailable: true,
                    GetCompactBrightness(display.DevicePath, percent),
                    isContrastAvailable: contrastAvailable,
                    GetCompactContrast(display.DevicePath, contrastPercent),
                    colorTemperaturePresets,
                    colorTemperatureRead is null ? null : checked((int)colorTemperatureRead.CurrentValue),
                    rotationAvailable,
                    rotationRead?.Rotation,
                    "External display · DDC/CI");
            }

            return new CompactMonitorViewModel(
                display.DevicePath,
                display.GdiDeviceName,
                display.FriendlyName,
                isBrightnessAvailable: false,
                brightnessPercent: 100,
                isContrastAvailable: contrastAvailable,
                GetCompactContrast(display.DevicePath, contrastPercent),
                colorTemperaturePresets,
                colorTemperatureRead is null ? null : checked((int)colorTemperatureRead.CurrentValue),
                rotationAvailable,
                rotationRead?.Rotation,
                _lastDdcProbes.Count == 0 && _lastWmiProbes.Count == 0
                    ? "Brightness has not been read yet"
                    : "Brightness control unavailable");
        }).ToArray();

        _compactBrightnessValues.Clear();
        foreach (var card in cards)
        {
            _compactBrightnessValues[card.DevicePath] =
                Math.Clamp((int)Math.Round(card.BrightnessPercent), 0, 100);
            _compactContrastValues[card.DevicePath] =
                Math.Clamp((int)Math.Round(card.ContrastPercent), 0, 100);
        }

        _updatingCompactControls = true;
        try
        {
            CompactMonitorList.ItemsSource = cards;
            MonitorList.ItemsSource = cards;
        }
        finally
        {
            _updatingCompactControls = false;
        }

        UpdateScheduleOptionsVisibility();
    }

    private double GetCompactBrightness(string devicePath, double verifiedPercent)
    {
        return _pendingBrightnessDevicePath is not null &&
               string.Equals(
                   _pendingBrightnessDevicePath,
                   devicePath,
                   StringComparison.OrdinalIgnoreCase)
            ? _pendingBrightnessPercent
            : Math.Round(verifiedPercent);
    }

    private double GetCompactContrast(string devicePath, double verifiedPercent)
    {
        return _pendingContrastDevicePath is not null &&
               string.Equals(_pendingContrastDevicePath, devicePath, StringComparison.OrdinalIgnoreCase)
            ? _pendingContrastPercent
            : Math.Round(verifiedPercent);
    }

    private static DdcVcpFeatureResult? GetSuccessfulFeatureRead(
        IReadOnlyList<MonitorDdcVcpFeatureInfo> probes,
        string devicePath) =>
        probes.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                devicePath,
                StringComparison.OrdinalIgnoreCase))?
            .PhysicalMonitors.FirstOrDefault(result =>
                result.Status == DdcVcpFeatureStatus.ReadSucceeded);

    private ColorTemperaturePresetViewModel[] GetColorTemperaturePresets(
        string devicePath)
    {
        var capabilities = _lastDdcCapabilities.FirstOrDefault(candidate => string.Equals(
            candidate.Display.DevicePath,
            devicePath,
            StringComparison.OrdinalIgnoreCase));
        var values = capabilities?.Capabilities.GetSupportedValues(
            NativeConstants.VcpCodeSelectColorPreset);
        return values is null
            ? []
            : values
                .Distinct()
                .Order()
                .Select(value => new ColorTemperaturePresetViewModel(
                    value,
                    VcpNames.GetFormattedValueName(
                        NativeConstants.VcpCodeSelectColorPreset,
                        value)))
                .ToArray();
    }

    private void UpdateCompactScheduleStatus()
    {
        var evaluation = EvaluateSavedSchedule(DateTimeOffset.Now);
        if (evaluation is null)
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

        if (_solarTimes?.Condition is SolarDayCondition.PolarDay or SolarDayCondition.PolarNight)
        {
            var state = _solarTimes.Condition == SolarDayCondition.PolarDay
                ? "polar day (Light)"
                : "polar night (Dark)";
            CompactScheduleStatusText.Text = _scheduleAutomationEnabled
                ? $"On · {state}"
                : $"Off · {state}";
            return;
        }

        CompactScheduleStatusText.Text = _scheduleAutomationEnabled
            ? string.Format(
                CultureInfo.CurrentCulture,
                "On · {0} at {1}",
                evaluation.NextMode,
                FormatTime(evaluation.NextTransitionTime))
            : string.Format(
                CultureInfo.CurrentCulture,
                "Off · Light {0}, Dark {1}",
                FormatTime(_savedThemeSchedule!.LightTime),
                FormatTime(_savedThemeSchedule.DarkTime));
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
        var notificationDiagnostics = _notificationAreaIcon?.GetDiagnostics();
        report.AppendLine("DisplayPilot active display-path report");
        report.Append("Captured: ").AppendLine(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        report.Append("OS: ").AppendLine(RuntimeInformation.OSDescription);
        report.Append("Process architecture: ").AppendLine(RuntimeInformation.ProcessArchitecture.ToString());
        report.Append("Notification-area icon active: ").AppendLine((_notificationAreaIcon is not null).ToString(CultureInfo.InvariantCulture));
        report.Append("Notification callbacks: ").AppendLine(
            (notificationDiagnostics?.CallbackCount ?? 0).ToString(CultureInfo.InvariantCulture));
        report.Append("Last notification code: ").AppendLine(
            notificationDiagnostics is null
                ? "None"
                : $"0x{notificationDiagnostics.Value.LastNotificationCode:X4}");
        report.Append("Last notification callback UTC: ").AppendLine(
            notificationDiagnostics?.LastCallbackUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "None");
        report.Append("Last notification menu command: ").AppendLine(
            notificationDiagnostics is null
                ? "None"
                : notificationDiagnostics.Value.LastMenuCommand.ToString(CultureInfo.InvariantCulture));
        report.Append("Recent notification codes: ").AppendLine(
            notificationDiagnostics is null
                ? "None"
                : string.Join(
                    ", ",
                    notificationDiagnostics.Value.RecentNotificationCodes.Select(code => $"0x{code:X4}")));
        report.Append("Context-menu requests: ").AppendLine(
            (notificationDiagnostics?.ContextMenuRequestCount ?? 0).ToString(CultureInfo.InvariantCulture));
        report.Append("Last context-menu stage: ").AppendLine(
            notificationDiagnostics?.LastContextMenuStage ?? "None");
        report.Append("Last context-menu error: ").AppendLine(
            notificationDiagnostics is null
                ? "None"
                : $"0x{unchecked((uint)notificationDiagnostics.Value.LastContextMenuError):X8}");
        report.Append("Window mode: ").AppendLine(_isCompactMode ? "Compact" : "Advanced");
        report.Append("Window visible: ").AppendLine(AppWindow.IsVisible.ToString(CultureInfo.InvariantCulture));
        report.Append("Start at sign-in: ").AppendLine(_startupRegistrationStatus.ToString());
        report.Append("Startup registration error: ").AppendLine(_startupRegistrationError ?? "None");
        report.Append("Display paths: ").AppendLine(monitors.Count.ToString(CultureInfo.InvariantCulture));
        report.Append("Theme apps: ").AppendLine(_themeState is null ? "Unknown" : _themeState.AppsUseLightTheme ? "Light" : "Dark");
        report.Append("Theme Windows: ").AppendLine(_themeState is null ? "Unknown" : _themeState.SystemUsesLightTheme ? "Light" : "Dark");
        report.Append("Last theme request: ").AppendLine(_lastThemeResult?.RequestedMode.ToString() ?? "None");
        report.Append("Last theme request verified: ").AppendLine(_lastThemeResult?.Succeeded.ToString() ?? "Not applicable");
        report.Append("Schedule light time: ").AppendLine(_customThemeSchedule is null ? "Invalid" : _customThemeSchedule.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        report.Append("Schedule dark time: ").AppendLine(_customThemeSchedule is null ? "Invalid" : _customThemeSchedule.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        report.Append("Saved schedule light time: ").AppendLine(_savedThemeSchedule?.LightTime.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Unavailable");
        report.Append("Saved schedule dark time: ").AppendLine(_savedThemeSchedule?.DarkTime.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Unavailable");
        report.Append("Schedule mode: ").AppendLine(_scheduleMode.ToString());
        report.Append("Schedule time zone: ").AppendLine(TimeZoneInfo.Local.Id);
        report.Append("Solar condition: ").AppendLine(_solarTimes?.Condition.ToString() ?? "Not applicable");
        report.Append("Solar sunrise: ").AppendLine(_solarTimes?.Sunrise?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable");
        report.Append("Solar sunset: ").AppendLine(_solarTimes?.Sunset?.ToString("O", CultureInfo.InvariantCulture) ?? "Unavailable");
        report.Append("Schedule preview mode: ").AppendLine(_lastScheduleEvaluation?.ActiveMode.ToString() ?? "Unavailable");
        report.Append("Schedule next mode: ").AppendLine(_lastScheduleEvaluation?.NextMode.ToString() ?? "Unavailable");
        report.Append("Schedule persisted: ").AppendLine(_scheduleWasLoadedFromDisk.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule settings error: ").AppendLine(_scheduleSettingsError ?? "None");
        report.Append("Schedule automatic writes enabled: ").AppendLine(_scheduleAutomationEnabled.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule brightness reduction enabled: ").AppendLine(_reduceBrightnessOnSchedule.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule brightness reduction active: ").AppendLine(_brightnessReductionActive.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule brightness restore values: ").AppendLine(_brightnessRestoreValues.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in _brightnessRestoreValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            report.Append("Schedule brightness restore: ")
                .Append(pair.Key)
                .Append(" = ")
                .Append(pair.Value.ToString(CultureInfo.InvariantCulture))
                .AppendLine("%");
        }
        report.Append("Schedule timer active: ").AppendLine(_themeScheduleTimer.IsArmed.ToString(CultureInfo.InvariantCulture));
        report.Append("Schedule timer due: ").AppendLine(_themeScheduleTimer.DueTime?.ToString("O", CultureInfo.InvariantCulture) ?? "None");
        report.Append("Schedule manual override until: ").AppendLine(_manualScheduleOverrideUntil?.ToString("O", CultureInfo.InvariantCulture) ?? "None");
        report.AppendLine("Privacy: device paths and WMI instance names can identify a local display instance; review before sharing");
        report.AppendLine(ddcProbes is null
            ? "DDC/CI commands issued: no"
            : _lastDdcVcpWriteResult is not null
                ? $"DDC/CI commands issued: brightness VCP 0x10 reads plus VCP 0x{_lastDdcVcpWriteResult.VcpCode:X2} write and verification read-back"
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

        if (_lastDdcVcpWriteResult is not null)
        {
            report.Append("Extended VCP write code: 0x")
                .AppendLine(_lastDdcVcpWriteResult.VcpCode.ToString("X2", CultureInfo.InvariantCulture));
            report.Append("Extended VCP write status: ").AppendLine(_lastDdcVcpWriteResult.Status.ToString());
            report.Append("Extended VCP requested raw: ").AppendLine(_lastDdcVcpWriteResult.RequestedRawValue.ToString(CultureInfo.InvariantCulture));
            report.Append("Extended VCP applied raw: ").AppendLine(_lastDdcVcpWriteResult.AppliedRawValue.ToString(CultureInfo.InvariantCulture));
            report.Append("Extended VCP verified raw: ").AppendLine(_lastDdcVcpWriteResult.VerifiedRawValue?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable");
            report.Append("Extended VCP requested percent: ").AppendLine(_lastDdcVcpWriteResult.RequestedPercent?.ToString(CultureInfo.InvariantCulture) ?? "Not applicable");
            report.Append("Extended VCP verified percent: ").AppendLine(_lastDdcVcpWriteResult.VerifiedPercent?.ToString(CultureInfo.InvariantCulture) ?? "Not applicable");
            report.Append("Extended VCP error: 0x").AppendLine(unchecked((uint)_lastDdcVcpWriteResult.ErrorCode).ToString("X8", CultureInfo.InvariantCulture));
            report.Append("Extended VCP message: ").AppendLine(_lastDdcVcpWriteResult.Message);
        }

        if (_lastRotationWriteResult is not null)
        {
            report.Append("Rotation write display: ").AppendLine(_lastRotationWriteResult.GdiDeviceName);
            report.Append("Rotation write status: ").AppendLine(_lastRotationWriteResult.Status.ToString());
            report.Append("Rotation requested: ").AppendLine(
                _lastRotationWriteResult.Rotation is null
                    ? "Unavailable"
                    : FormatRotation(_lastRotationWriteResult.Rotation.Value));
            report.Append("Rotation native result: ").AppendLine(
                _lastRotationWriteResult.NativeResult?.ToString(CultureInfo.InvariantCulture) ?? "None");
            report.Append("Rotation write message: ").AppendLine(_lastRotationWriteResult.Message ?? "None");
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

            var rotationRead = _lastRotationReads.FirstOrDefault(candidate =>
                string.Equals(candidate.GdiDeviceName, monitor.GdiDeviceName, StringComparison.OrdinalIgnoreCase));
            report.Append("Rotation status: ").AppendLine(rotationRead?.Status.ToString() ?? "Not read");
            report.Append("Rotation current: ").AppendLine(
                rotationRead?.Rotation is null
                    ? "Unavailable"
                    : FormatRotation(rotationRead.Rotation.Value));
            report.Append("Rotation native result: ").AppendLine(
                rotationRead?.NativeResult?.ToString(CultureInfo.InvariantCulture) ?? "None");
            report.Append("Rotation message: ").AppendLine(rotationRead?.Message ?? "None");

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

            AppendVcpFeatureDiagnostics(report, "Contrast", monitor, _lastContrastProbes);
            AppendVcpFeatureDiagnostics(report, "Color temperature", monitor, _lastColorTemperatureProbes);
            var capabilities = _lastDdcCapabilities.FirstOrDefault(candidate => string.Equals(
                candidate.Display.DevicePath,
                monitor.DevicePath,
                StringComparison.OrdinalIgnoreCase));
            report.Append("MCCS capabilities: ").AppendLine(capabilities is null
                ? "not read"
                : capabilities.Succeeded
                    ? "read"
                    : $"failed (0x{unchecked((uint)capabilities.Win32Error):X8})");
            if (capabilities?.Succeeded == true)
            {
                report.Append("MCCS VCP codes: ").AppendLine(string.Join(", ", capabilities.Capabilities.GetVcpCodesAsHexStrings()));
                report.Append("MCCS color presets: ").AppendLine(string.Join(", ",
                    capabilities.Capabilities.GetSupportedValues(NativeConstants.VcpCodeSelectColorPreset) ?? []));
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

    private static void AppendVcpFeatureDiagnostics(
        StringBuilder report,
        string featureName,
        MonitorDisplayInfo monitor,
        IReadOnlyList<MonitorDdcVcpFeatureInfo> probes)
    {
        var probe = probes.FirstOrDefault(candidate => string.Equals(
            candidate.Display.DevicePath,
            monitor.DevicePath,
            StringComparison.OrdinalIgnoreCase));
        if (probe is null)
        {
            report.Append(featureName).AppendLine(" VCP: not probed");
            return;
        }

        foreach (var physicalMonitor in probe.PhysicalMonitors)
        {
            report.Append(featureName).Append(" VCP status: ").AppendLine(physicalMonitor.Status.ToString());
            report.Append(featureName).Append(" VCP current: ").AppendLine(physicalMonitor.CurrentValue.ToString(CultureInfo.InvariantCulture));
            report.Append(featureName).Append(" VCP maximum: ").AppendLine(physicalMonitor.MaximumValue.ToString(CultureInfo.InvariantCulture));
            report.Append(featureName).Append(" VCP error: 0x").AppendLine(unchecked((uint)physicalMonitor.Win32Error).ToString("X8", CultureInfo.InvariantCulture));
        }
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
