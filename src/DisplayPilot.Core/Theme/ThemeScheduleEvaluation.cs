// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Core.Theme;

public sealed record ThemeScheduleEvaluation(
    ThemeMode ActiveMode,
    TimeOnly NextTransitionTime,
    ThemeMode NextMode,
    TimeSpan TimeUntilNextTransition);
