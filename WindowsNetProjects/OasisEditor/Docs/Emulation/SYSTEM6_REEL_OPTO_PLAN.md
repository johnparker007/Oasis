# System6 Reel Opto Configuration Plan

## Context

System6 native DLL emulation now boots, audio works, and the editor can pause, resume and reset the core. The next required machine-specific configuration is reel opto setup.

The native System6 core exposes per-reel functions for configuring reel position and opto behaviour:

```text
SetSteps(UINT8 reelNum, UINT8 steps)
SetOptoStart(UINT8 reelNum, UINT8 start)
SetOptoEnd(UINT8 reelNum, UINT8 end)
SetOptoInvert(UINT8 reelNum, UINT8 state)
```

The reference reel implementation stores these values per reel and uses them when deriving the opto signal from the current reel split/position.

The initial values supplied by the emulator author are:

```text
steps = 96
optoStart = 5
optoEnd = 7
optoInvert = false
```

These should be configurable per reel in the Oasis project settings.

## Goal

Add project-level System6 reel opto configuration and apply it to the native DLL backend during startup.

MAME behaviour must remain unchanged.

## Project model

Extend the project-level System6 native settings with reel opto entries.

Suggested model:

```csharp
public sealed class System6ReelOptoSettings
{
    public int ReelIndex { get; set; }
    public int Steps { get; set; } = 96;
    public int OptoStart { get; set; } = 5;
    public int OptoEnd { get; set; } = 7;
    public bool OptoInvert { get; set; } = false;
}
```

Suggested container:

```csharp
public List<System6ReelOptoSettings> ReelOptos { get; } = [];
```

Prefer storing eight reels by default, because the native reel implementation has eight reel slots. The UI may initially show 1-based labels while storing 0-based reel indices for the DLL calls.

## Persistence

Persist reel opto settings in `.oasisproj` under the existing native System6 settings area.

Suggested JSON shape:

```json
"nativeEmulation": {
  "system6": {
    "programRomPaths": [],
    "soundRomPaths": [],
    "flashSwitch": false,
    "reelOptos": [
      { "reelIndex": 0, "steps": 96, "optoStart": 5, "optoEnd": 7, "optoInvert": false }
    ]
  }
}
```

Do not break existing project files. Missing reel opto settings should resolve to sensible defaults.

## Project Settings UI

Add a section under the existing Native DLL ROMs / System6 ROMs fields:

```text
System6 Reel Optos

Reel | Steps | Opto Start | Opto End | Inverted
1    | 96    | 5          | 7        | false
2    | 96    | 5          | 7        | false
...
```

MVP can use a simple grid or repeated rows. It does not need a polished add/remove UI.

Add a button:

```text
Reset Reel Optos To Defaults
```

Default each reel to:

```text
steps = 96
optoStart = 5
optoEnd = 7
optoInvert = false
```

## Native ABI

Bind the additional exports in `System6NativeLibrary`:

```text
SetSteps
SetOptoStart
SetOptoEnd
SetOptoInvert
```

Use the same calling convention and small integer types already proven for the System6 ABI.

Likely C# delegate shape:

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void SetStepsDelegate(byte reelNum, byte steps);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void SetOptoStartDelegate(byte reelNum, byte start);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void SetOptoEndDelegate(byte reelNum, byte end);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
private delegate void SetOptoInvertDelegate(byte reelNum, byte state);
```

Use `byte` values after validating/clamping project settings into the valid range 0-255.

## Startup order

Apply reel opto configuration after ROM loading and before the first run-loop frame.

Recommended order:

```text
Initialise
LoadROM
LoadSoundROM if configured
SetSteps / SetOptoStart / SetOptoEnd / SetOptoInvert for each configured reel
Reset
Start run loop
```

If the DLL author recommends setting optos after Reset, update the order, but keep it before the first Run call.

## Validation

Validate before startup:

- steps must be between 1 and 255;
- optoStart must be between 0 and 255;
- optoEnd must be between 0 and 255;
- optoStart should be less than optoEnd for the normal case;
- reel index must be within native supported range, initially 0-7.

If validation fails, do not start the native backend.

## Diagnostics

Log native startup stages:

```text
System6 reel opto 1: steps=96 start=5 end=7 inverted=false
```

Avoid logging every frame. Log only setup/configuration.

## Tests

Add tests for:

- default reel opto settings;
- project JSON round-trip;
- project settings setters save metadata;
- launch request carries reel opto settings;
- backend validates bad opto values;
- backend calls SetSteps, SetOptoStart, SetOptoEnd and SetOptoInvert before the run loop;
- MAME path remains unchanged.

## Manual verification

1. Open a System6 project.
2. Confirm Project Settings shows the reel opto section.
3. Confirm defaults are 96, 5, 7, not inverted.
4. Start native System6 emulation.
5. Confirm output log shows the configured opto values being applied.
6. Confirm the machine boots and reels behave better than before.
7. Confirm MAME projects still use the existing MAME path.
