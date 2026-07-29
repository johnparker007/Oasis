# Fabric migration test plan

## Automated managed coverage

ABI tests verify x64 sizes/offsets, inline capacities and Cdecl delegate metadata. Managed fake-session tests should cover request mapping, lifecycle/failure cleanup, elapsed timing and baselines, persistent inputs, immutable snapshot publication/change detection, and audio frame conversion. Wrapper validation covers absolute existing paths, role-local contiguous slots and four-ROM limits.

## Native and hardware coverage

A source-built fake native runtime was not added because the repository's current WPF CI/tooling does not contain a portable Windows native-DLL build step. On Windows, test missing exports, UTF-8 fields, typed resources, configuration bytes, two-call errors, snapshot retry, frame semantics, destruction and module lifetime with a purpose-built fake DLL.

Real `FabricRuntime.dll`, Amber API v2 DLL, proprietary ROM, audible playback, and long-running timing tests were not executed in the Codex Linux environment. Before release, exercise valid and invalid dual-path preferences, start/reset/pause/resume/input/shutdown, lamp logical state versus brightness, signed reels, punctuation attributes, unchanged segment masks, partial audio reads, and repeated startup-failure cleanup.

## Hardening coverage in PR #589

Managed behavioural tests now exercise exact launch identifiers and paths, typed ROM ordering and gap rejection, raw percentage validation, field-level Amber reel/coin mapping, retained fractional elapsed-time conversion, serialized pump/reset access, deterministic pump-failure cleanup, and single shutdown/session/runtime/audio disposal. Factory tests cover enabled selection, invalid enabled configuration, and retain the existing direct-backend and MAME-fallback tests.

The wrapper now transfers ownership only after capability discovery and live-session registration, validates successful snapshot headers/counts/capacities/pointers/UTF-8, reuses geometrically grown native arrays, and validates interleaved audio sample capacity before native reads. A source-built native DLL, real Fabric/Amber DLLs, ROM execution, and audible Windows playback remain Windows-only and were not executed here.

## Windows native integration harness

`OasisEditor.NativeIntegrationTests` is an opt-in Windows test project. Every native test dynamically skips when its prerequisite is absent, so ordinary CI does not require Fabric or proprietary artifacts.

Environment variables:

- `FABRIC_RUNTIME_DLL`: absolute path to the real `FabricRuntime.dll`; enables runtime loading, ABI rejection, error, unload, and provider tests.
- `AMBER_FAKE_API_V2_DLL`: absolute path to a test Amber API v2 backend; enables full real-Fabric provider negotiation, typed dummy-ROM/configuration marshalling, lifecycle, input, snapshot, audio, and stress tests. Dummy ROM files are generated and deleted by the test, so proprietary ROMs are not required.
- `AMBER_API_V2_DLL`: optional absolute path to a real Amber API v2 DLL.
- `AMBER_TEST_ROM_DIRECTORY`: optional real-ROM root containing `program/` and `sound/` subdirectories. Files are sorted ordinally and mapped to slots 0..3.
- `FABRIC_LAYOUT_PROBE_EXE`: path to the x64 probe built from `NativeProbe/fabric_layout_probe.c`. From a Visual Studio Developer PowerShell, run `NativeProbe/build-layout-probe.ps1`, then set this variable to the emitted executable.

Run on Windows with:

```powershell
pwsh .\OasisEditor.NativeIntegrationTests\NativeProbe\build-layout-probe.ps1
$env:FABRIC_LAYOUT_PROBE_EXE = Resolve-Path .\OasisEditor.NativeIntegrationTests\NativeProbe\fabric_layout_probe.exe
$env:FABRIC_RUNTIME_DLL = 'C:\path\to\FabricRuntime.dll'
$env:AMBER_FAKE_API_V2_DLL = 'C:\path\to\FakeAmberApiV2.dll'
dotnet test .\OasisEditor.NativeIntegrationTests\OasisEditor.NativeIntegrationTests.csproj -c Release
```

The real-Amber test skips unless both real-Amber variables exist. The fake-provider tests skip unless both Fabric and fake Amber are configured. The layout test skips unless its probe executable is configured. Performance results are written to test output and never used as pass/fail thresholds.

Known limitations: Fabric's published ABI has no provider machine-enumeration export, so negotiation is validated by successful creation of the exact `amber-api-v2` / `jpm-system6` session rather than a separate enumeration call. OS-level leak counters are not portable in xUnit; lifecycle stress instead exercises 200 complete load/session/unload cycles and relies on native diagnostics, process stability, and Windows Application Verifier/ASan when developers choose to attach them.
