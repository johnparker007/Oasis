# Task: Refresh Oasis Editor System6 native DLL integration for updated OasisEmulator API

## Context

The editor lives under `WindowsNetProjects/OasisEditor` in this repository.

The native System 6 emulator DLL source lives in a separate repository:

- `https://github.com/johnparker007/OasisEmulator`

The DLL author has updated that repo to provide a cleaner and more efficient API for editor integration. A new DLL has already been built locally by the project owner. This task is to update the Oasis Editor native System 6 backend so it uses the latest OasisEmulator exported API instead of carrying forward assumptions from the older legacy export set.

## Current editor-side integration to review

Primary files:

- `OasisEditor/Emulation/EmulationBackendAbstractions.cs`
- `OasisEditor/Emulation/Native/System6NativeBackend.cs`
- `OasisEditor/Emulation/Native/System6NativeLibrary.cs`
- related tests under `OasisEditor.Tests`, especially native backend/library/factory tests

The editor backend abstraction currently exposes:

- lifecycle: `StartAsync`, `StopAsync`, `PauseAsync`, `ResumeAsync`, `ResetAsync`
- input: `SetInputStateAsync(InputDefinitionModel inputDefinition, bool isPressed, ...)`
- outputs/events: `LampChanged`, `ReelChanged`, `SegmentChanged`, `VfdBrightnessChanged`, `DotMatrixChanged`

The existing `System6NativeBackend` currently:

- loads the DLL through `System6NativeLibrary`
- manually binds a large set of native exports
- loads program ROMs and sound ROMs as separate path slots
- configures reel optos and coins through several individual setters
- runs the core on a 1000 Hz pump using `SYSTEM6Run(cycles)`
- polls outputs separately, usually at 60 Hz
- emits Oasis editor events after mapping native lamp/reel/segment/VFD values
- sends non-coin button input via `SYSTEM6TurnSwitchOn` / `SYSTEM6TurnSwitchOff`

The current library wrapper binds legacy-style exports including:

- `SYSTEM6Initialise`
- `SYSTEM6LoadROM`
- `SYSTEM6LoadSoundROM`
- `SYSTEM6Reset`
- `SYSTEM6Run`
- `SYSTEM6Shutdown`
- `SYSTEM6GetLampsOn`
- `SYSTEM6GetLampBrightness`
- `SYSTEM6GetPosOut`
- `SYSTEM6GetAlphaSegments`
- `SYSTEM6GetAlphaBright`
- `SYSTEM6UpdateSegs`
- `SYSTEM6GetSegOn`
- `SYSTEM6GetSegBright`
- `SYSTEM6TurnSwitchOn`
- `SYSTEM6TurnSwitchOff`
- many configuration setters for reel optos, coins and percent switches

This current shape is probably no longer the best integration point if OasisEmulator now exposes batched state/input/output structs or a higher-level API.

## Required first step

Before changing editor code, inspect the latest `johnparker007/OasisEmulator` source and identify the current public DLL ABI.

Look for:

- exported function declarations / macros
- C ABI header files
- sample host code or tests
- release notes / README API notes
- structs for machine configuration, inputs, outputs, lamps, reels, seven-seg, alpha/VFD, meters, coin lockouts, etc.

Produce a short local note in the PR/commit summary mapping:

| Area | Current editor usage | New OasisEmulator API | Required editor change |
| --- | --- | --- | --- |
| lifecycle | initialise/load/reset/run/shutdown | TBD from OasisEmulator | TBD |
| ROM loading | four path slots per ROM group | TBD | TBD |
| input | on/off switch calls | TBD | TBD |
| lamps | per-lamp polling | TBD | TBD |
| reels | per-reel position polling | TBD | TBD |
| seven-seg/alpha | per-cell polling | TBD | TBD |
| timing | editor run loop passes cycles | TBD | TBD |

## Implementation goals

1. Update `System6NativeLibrary` to bind the new OasisEmulator API.
   - Prefer a small, explicit wrapper over reflection/stringly typed logic.
   - Keep optional export probing only where backwards compatibility is deliberately required.
   - Ensure delegate signatures exactly match the native calling convention, parameter width, signedness, packing, and lifetime rules.
   - If the new API uses structs, define C# interop structs with explicit layout where needed and add comments linking them to the native ABI names.

2. Update `System6NativeBackend` to use the new API efficiently.
   - Avoid many small native calls per frame if the DLL now exposes batched outputs.
   - Keep the public `IEmulationBackend` contract unchanged unless a broader editor change is genuinely required.
   - Continue to emit existing editor events for lamps, reels, segments, VFD brightness and future dot matrix outputs.
   - Preserve the MAME/native normalization boundary: downstream rendering should not need to know the backend source.

3. Revisit timing ownership.
   - Determine whether the editor should continue calling a `Run(cycles)`-style function, or whether the new DLL owns timing internally and exposes a tick/frame/pump function.
   - Keep pause/resume/reset behavior compatible with the existing editor UI.
   - Keep diagnostics useful but remove obsolete startup-stage probes that only existed to debug the old DLL.

4. Revisit input handling.
   - Map `InputDefinitionModel` switch IDs to the new input API.
   - Confirm active-high/active-low expectations.
   - Preserve current behavior where coin inputs are ignored by `SetInputStateAsync` if coin handling is configured elsewhere, unless the new API expects coin inputs to use the same path.

5. Revisit output mapping.
   - Lamps: use brightness if the new API exposes brightness; otherwise preserve on/off `0` or `255` event values.
   - Reels: keep Oasis event reel IDs one-based if the editor/rendering expects that.
   - Segments: retain existing `MameSegmentOutputType` values or add a clearly named native output type only if required.
   - Alpha/VFD: keep native alpha mask mapping in one boundary layer.
   - Seven-segment: remove hard-coded stride assumptions if the new API provides digit masks directly.

6. Clean up obsolete compatibility code.
   - Remove stale optional legacy export handling if the new DLL is now the supported target.
   - Remove obsolete environment variables and staged startup diagnostics unless they still serve a useful troubleshooting purpose.
   - Keep concise timing/output diagnostics.

7. Update tests.
   - Update native library wrapper tests for the new exports/signatures.
   - Update backend tests using a fake `ISystem6NativeLibrary` to cover lifecycle, input mapping, batched output polling, event emission, reset and shutdown.
   - Add regression tests around lamp brightness, reel indexing/normalization, alpha/segment mask mapping and input press/release.

8. Build and test.
   - Run the Oasis Editor test suite.
   - Run the editor with the locally built new DLL and at least one System 6 ROM configuration.
   - Verify: startup, reset, pause/resume, switch inputs, lamps, reels, alpha/seven-seg output, shutdown/restart without leaked DLL handles.

## Suggested code structure

If the new API is materially different, consider replacing the current wrapper with a clearer split:

- `System6NativeApi` or `OasisEmulatorNativeApi`: raw P/Invoke/delegate binding only
- `System6NativeLibrary`: safe-ish managed wrapper over the raw ABI
- `System6NativeBackend`: adapts managed wrapper to `IEmulationBackend`

Keep emulator-specific logic inside `Emulation/Native`; do not leak native ABI structs into view models or rendering code.

## Acceptance criteria

- Oasis Editor builds successfully.
- Tests pass after updating expectations.
- The editor can start and stop the new locally built OasisEmulator DLL.
- Inputs sent from the editor reach the emulator.
- Lamp state changes are visible through existing `LampChanged` events.
- Reel position changes are visible through existing `ReelChanged` events.
- Segment/VFD output still renders where supported by the DLL.
- Old DLL-specific diagnostic scaffolding is removed or clearly marked as compatibility-only.
- Any remaining legacy export fallback is intentional and documented.

## Starting Codex prompt

Use this as the initial Codex instruction:

```text
We are working in `WindowsNetProjects/OasisEditor` in the `johnparker007/Oasis` repo. The editor currently has a native System 6 backend that talks to an older OasisEmulator/System6 DLL API through `OasisEditor/Emulation/Native/System6NativeBackend.cs` and `System6NativeLibrary.cs`.

The updated DLL source is in `https://github.com/johnparker007/OasisEmulator`. The new DLL has already been built locally. Inspect the latest OasisEmulator source first and identify its current exported C ABI / host API. Then update Oasis Editor to use that new cleaner API.

Do not preserve old legacy export assumptions unless they are still required. Keep the editor-facing `IEmulationBackend` contract stable if possible. Inputs, lamps, reels, segments and VFD brightness should continue to flow through the existing editor event model. Prefer batched output/state APIs if the new DLL provides them.

Please read `WindowsNetProjects/OasisEditor/Docs/Emulation/TASK_SYSTEM6_NATIVE_DLL_API_REFRESH.md` before making changes. Implement the wrapper/backend/test updates, remove obsolete compatibility scaffolding, and run the relevant tests/build.
```
