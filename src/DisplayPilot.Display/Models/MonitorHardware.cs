// Copyright (c) Microsoft Corporation
// Portions derived from PowerToys; MIT terms retained in LICENSE.
namespace DisplayPilot.Display.Models;
public partial class Monitor
{
        /// <summary>
        /// Gets or sets unique identifier for all purposes: UI lookups, IPC, persistent storage, and handle management.
        /// </summary>
        /// <remarks>
        /// Format: the Windows DevicePath with the trailing device-class GUID stripped,
        /// e.g. <c>\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID12345</c>. Stable across
        /// reboots, sleep/wake, and identical-monitor reordering by Windows.
        /// </remarks>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets physical monitor handle (for DDC/CI)
        /// </summary>
        public IntPtr Handle { get; set; } = IntPtr.Zero;

        /// <summary>
        /// Gets or sets instance name (used by WMI)
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets communication method (DDC/CI, WMI, HDR API, etc.)
        /// </summary>
        public string CommunicationMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets supported control methods
        /// </summary>
        public MonitorCapabilities Capabilities { get; set; } = MonitorCapabilities.None;

        /// <summary>
        /// Gets or sets the set of current values successfully read from the device during
        /// discovery. Callers (e.g. the CLI) use this to avoid reporting a default/stale value
        /// as the live "before" value. The GUI ignores this.
        /// </summary>
        public MonitorReadFlags ReadValues { get; set; } = MonitorReadFlags.None;

        /// <summary>
        /// Gets or sets raw DDC/CI capabilities string (MCCS format)
        /// </summary>
        public string? CapabilitiesRaw { get; set; }

        /// <summary>
        /// Gets or sets parsed VCP capabilities information
        /// </summary>
        public VcpCapabilities? VcpCapabilitiesInfo { get; set; }


}
