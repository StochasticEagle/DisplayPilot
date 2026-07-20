// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Core.Theme;

public sealed record ThemeScheduleEvaluation(
    ThemeMode ActiveMode,
    TimeOnly NextTransitionTime,
    ThemeMode NextMode,
    TimeSpan TimeUntilNextTransition);
