// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Core.Theme;

public sealed record SolarLocation
{
    public SolarLocation(double latitude, double longitude, string? label = null)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        Latitude = latitude;
        Longitude = longitude;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public string? Label { get; }
}
