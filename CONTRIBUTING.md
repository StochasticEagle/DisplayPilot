# Contributing to DisplayPilot

Thank you for contributing to DisplayPilot. The project accepts focused bug fixes,
tests, documentation improvements, and features that fit its established scope.

## Before opening a pull request

1. Search existing issues and pull requests for related work.
2. Use an issue template for bugs, feature proposals, or DDC/CI hardware reports.
3. Discuss substantial behavior or architecture changes before implementation.
4. Do not include secrets, precise location data, unredacted diagnostic reports,
   or other personal information.

Security vulnerabilities must follow [SECURITY.md](SECURITY.md) and must not be
reported through a public issue.

## Development requirements

- Windows 10 or Windows 11 x64
- .NET 10 SDK
- Hardware relevant to any display-control change
- Inno Setup 6 when modifying or validating the installer

Build and test from PowerShell:

```powershell
dotnet restore DisplayPilot.slnx
dotnet build DisplayPilot.slnx --configuration Release --no-restore
dotnet test DisplayPilot.slnx --configuration Release --no-build
```

Warnings are treated as errors. New domain behavior should include focused tests.
Hardware-specific changes should also include the applicable manual verification
results from the documents under `docs/`.

## Branches and pull requests

- `master` is the default and release branch.
- Use a descriptive development branch, such as `development/ddc-retry-fix`.
- Keep commits focused and use imperative commit messages.
- Limit each pull request to one coherent change.
- Describe user impact, implementation details, tests performed, and remaining
  hardware or operating-system coverage.
- Do not commit generated build output, installers, runtime packages, or private
  diagnostic files.

## Code and compatibility expectations

- Preserve Windows 10 and Windows 11 x64 compatibility.
- Keep monitor features capability-driven; never assume every DDC/CI display
  implements the same VCP codes or value mappings.
- Verify writes by reading values back when the interface permits it.
- Keep UI operations responsive and serialize hardware writes where required.
- Preserve per-user privacy and avoid telemetry or third-party solar services.
- Retain upstream copyright and MIT license headers on PowerToys-derived source.
- Do not add ARM64-specific claims without corresponding hardware validation.

## Hardware reports

DDC/CI behavior varies by monitor, connection, adapter, dock, GPU, and driver.
Useful reports identify each part of that path and state whether DDC/CI is enabled
in the monitor's on-screen menu. Redact device paths and WMI instance names before
posting diagnostic excerpts publicly.

## Review and acceptance

Passing automated checks is required but does not replace Windows and hardware
validation. A contribution may be declined when it expands intentionally excluded
scope, cannot be tested safely, adds unnecessary background activity, or creates
unsupported packaging and maintenance obligations.
