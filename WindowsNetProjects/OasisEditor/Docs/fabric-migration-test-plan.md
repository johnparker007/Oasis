# Fabric-only Amber migration test plan

The supported runtime chain is:

```text
OasisEditor -> FabricRuntime.dll -> production Amber provider DLL
```

## Automated unit tests

Run on Windows with the .NET/WPF toolchain:

```powershell
dotnet test .\OasisEditor.Tests\OasisEditor.Tests.csproj
```

The managed suite covers backend selection, configuration validation, Fabric ABI layout, the fixed 1 ms scheduler, bounded catch-up, ROM and provider configuration, input routing and release, output snapshots, partial audio reads, audio buffering, and failure cleanup. It also asserts the `amber` backend identifier and `jpm-system6` machine identifier.

## Windows-only native integration tests

With the required native binaries configured, run:

```powershell
dotnet test .\OasisEditor.NativeIntegrationTests\OasisEditor.NativeIntegrationTests.csproj
```

These tests validate the native Fabric ABI/layout and runtime/provider integration. They are separate from the managed unit suite because they require Windows and the native deployment.

## Manual end-to-end verification

Historical verification after PR #594 used the former experimental Amber API v2 provider path. The current standalone Fabric runtime instead reaches the production `amber` backend.

The following focused checks remain for this verification pass:

1. Open OasisEditor and confirm Preferences exposes only the Fabric runtime path, production Amber provider path, and audio buffer setting for emulation.
2. Open an Impact/System 6 project; start emulation and verify lamps, reels, segments/VFD, audio, and input.
3. Stop and restart emulation, then verify focus loss releases every asserted input.
4. Verify a missing Fabric runtime produces an actionable Fabric error and no fallback.
5. Verify a missing Amber provider produces an actionable provider error and no fallback.
6. Verify Epoch reports unsupported before Fabric path validation.
7. Confirm no `mame.exe` process starts and no MAME setup, debugger, download, or `.lay` export UI exists.
8. Inspect process modules with suitable Windows diagnostics: OasisEditor loads `FabricRuntime.dll`, does not directly load the production Amber provider, and Fabric loads and uses that provider.
