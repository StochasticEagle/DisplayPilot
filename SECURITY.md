# Security policy

## Supported versions

DisplayPilot is currently beta software. Security fixes are provided for the
current release line only.

| Version | Security support |
| --- | --- |
| 0.9.x beta | Supported |
| Earlier versions | Not supported |

## Reporting a vulnerability

Do not disclose suspected vulnerabilities, exploit details, sensitive logs, or
personal information in a public issue.

Use GitHub's private vulnerability reporting interface from the repository
**Security** tab and select **Report a vulnerability**. Include:

- affected DisplayPilot version;
- Windows version and build;
- concise reproduction steps;
- expected and observed behavior;
- security impact and realistic attack conditions;
- relevant source locations or a minimal proof of concept; and
- whether the issue has been disclosed elsewhere.

If private vulnerability reporting is unavailable, open a public issue containing
no vulnerability details and request a private contact channel from the maintainer.

Reports will be acknowledged when reviewed. Validation and remediation timelines
depend on severity, reproducibility, and maintainer availability. Confirmed issues
will be coordinated privately until a fix and disclosure plan are ready.

## Scope

Security reports should concern DisplayPilot source, installer behavior, local
privilege boundaries, settings handling, or dependencies. General Windows,
monitor-firmware, graphics-driver, DDC/CI, WMI, or GitHub platform vulnerabilities
should be reported to their respective vendors unless DisplayPilot introduces or
materially amplifies the issue.

## Sensitive diagnostics

DisplayPilot diagnostic reports omit saved solar coordinates but contain stable
display paths and WMI instance names. Redact hardware identifiers and any other
personal or environment-specific information before sharing a report.
