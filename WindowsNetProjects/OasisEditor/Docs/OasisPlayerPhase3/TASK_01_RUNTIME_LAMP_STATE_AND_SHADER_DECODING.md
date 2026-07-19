# Task 01: Runtime Lamp State and Shader Decoding — Complete

## Objective

Implement a machine-owned runtime lamp-state model, bind it to Face materials, and extend `Oasis/Face` so manual lamp brightness changes illuminate the correct exported Face pixels without emulator integration.

## Implementation summary

- Added `RuntimeLampState` with normalized 0..1 brightness indexed by Oasis lamp number.
- Valid lamp numbers are 1..255, matching the current Editor writer and native backend practical maximum. Lamp 0 remains the invalid/unassigned sentinel.
- Added a machine-owned `RuntimeLampStateTexture`, a 256x1 point-filtered linear texture with brightness in the red channel. Index 0 is reserved/zero; lamp N samples texel N.
- `RuntimeMachine` owns CPU lamp state and the GPU lamp texture; Faces only bind references to that machine-owned texture.
- `RuntimeMachine.ApplyDynamicState()` uploads only when the lamp state is dirty, coalescing multiple changes into one upload.
- `RuntimeFaceMaterialFactory` binds the machine lamp texture and centralizes the new shader property IDs.
- `Oasis/Face` now decodes RGB lamp ID/weight contributions, gates by the runtime mask formula, multiplies artwork by static brightness plus lamp light, clamps RGB, and preserves artwork alpha.
- Added `RuntimeMachineLampUpdater` to upload dirty lamp data during `LateUpdate`.
- Added guarded `RuntimeLampDevelopmentControls` in Editor/development builds for deterministic manual verification.

## Development controls

In Preview/Player development builds:

- `[` selects the previous lamp number.
- `]` selects the next lamp number.
- `0` sets the selected lamp to 0%.
- `1` sets the selected lamp to 25%.
- `2` sets the selected lamp to 50%.
- `3` sets the selected lamp to 100%.
- `C` clears all lamps.
- Hold `L` to cycle the configured small lamp range.

## Backward compatibility

Faces missing lookup textures remain non-fatal and keep static artwork. Missing lookup texture defaults sample as black, so no dynamic light is added. Machine builds without Faces continue to load. No schema changes were introduced.

## Known limitations

- No emulator bridge is implemented.
- No support is added for speculative `lampIds1`/`lampWeights1` textures because the current production exporter does not write them.
- Tray IDs are decoded/documented but do not bank lamp state because the authoritative current Editor preview uses lamp ID directly.

## Next checkpoint

`TASK_02_EMULATION_LAMP_BRIDGE.md`
