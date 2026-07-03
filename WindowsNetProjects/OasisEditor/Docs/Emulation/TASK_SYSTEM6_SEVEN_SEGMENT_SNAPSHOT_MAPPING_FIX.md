# Task: Fix System 6 snapshot seven-segment / alpha test fallout

## Context

The Editor now uses the new OasisEmulator DLL and the new snapshot-style native output API. Emulation is broadly working, including audio, but one test is failing and seven-segment displays in a test machine appear scrambled.

Failing test:

```text
OasisEditor.Tests.System6NativeBackendTests.StartAsyncOneRunOnlyPollsAlphaSegmentsAndPublishesNativeAlphaSegmentMasks
Assert.Equal() Failure: Collections differ
Expected: int[]     [0, 1, 2, 3, 4, ...]
Actual:   List<int> [0, 1]
```

Relevant files:

- `OasisEditor/Emulation/Native/System6NativeBackend.cs`
- `OasisEditor/Emulation/Native/System6OutputSnapshot.cs`
- `OasisEditor.Tests/System6NativeBackendTests.cs`
- latest OasisEmulator API/header/source, especially the output snapshot structs and LED display bit ordering

## Findings so far

The failing alpha test looks stale after the move to `GetOutputSnapshot()`.

Old behavior:

- Backend called `GetAlphaSegments(0..15)`.
- The fake native library recorded every index in `AlphaSegmentIndices`.
- The test asserted that indices `0..15` were polled.

Current behavior:

- Backend calls `library.GetOutputSnapshot()` once.
- `PollAlphaOutputs()` reads `snapshot.GetAlphaSegmented(0)` and then reads all 16 `alpha.Segments[index]` directly from the snapshot.
- The fake library currently only adds indices that were explicitly populated into the snapshot, so the test sees `[0, 1]` rather than `0..15`.

This is probably a test expectation/fake-library issue, not necessarily a production alpha-display bug.

Separate but related: seven-segment rendering may have a real mapping issue.

Current snapshot struct:

- `System6NativeLedDisplayState` has `uint OnOff` and `float Brightness`.
- `System6NativeOutputSnapshot` contains `LedDisplays[LedDisplayCapacity * 8]`.
- Backend `PollSevenSegmentOutputs()` currently forwards `ledDisplay.OnOff` directly as the editor `MameSegmentOutputType.Digit` mask.

Older legacy path/test assumptions:

- Old native API exposed segment cells, where a digit occupied a 16-cell block.
- The editor constructed a digit mask by reading native indices `displayId * 16 + bit`.
- The test still creates fake seven-segment state by setting cells like `32..37` and `80 + 1`, then the fake snapshot groups those cells by `/ 16` into `LedDisplayState.OnOff`.

Now that the snapshot API exposes `LedDisplayState.OnOff`, that may already be a digit-level mask. The editor must confirm whether its bit order is already the Oasis/MAME digit bit order, or whether it needs a native-to-Oasis remap before raising `SegmentChanged`.

## Required investigation

Inspect the latest `johnparker007/OasisEmulator` source/header and confirm:

1. What exactly `LedDisplays[index].OnOff` represents.
2. Whether `OnOff` bit order is:
   - already Oasis/MAME digit mask order, or
   - native System 6 segment order, or
   - physical LED output order requiring mapping.
3. Whether `LedDisplay` index corresponds directly to editor/machine seven-segment display id.
4. Whether decimal point / comma bits are included and which bit positions they use.
5. Whether alpha-segment masks in `AlphaSegmented.Segments[]` still require `System6AlphaSegmentMapper.MapNativeMaskToOasisMask()`.

Do not guess from the old 16-cell stride code. The OasisEmulator snapshot header/source is the source of truth.

## Likely implementation changes

### 1. Update the failing alpha test

The production backend no longer calls `GetAlphaSegments(0..15)`, so the test should stop asserting `library.AlphaSegmentIndices == Enumerable.Range(0, 16)` unless the fake snapshot is deliberately changed to record reads differently.

Better assertions:

- `GetOutputSnapshot` was called.
- 16 `SegmentChanged` events are emitted for native alpha cells, if that is the intended first-snapshot behavior.
- Cell 0 maps `0x0001 -> 0x0001` as `NativeAlpha`.
- Cell 1 maps `0x8002 -> 0x2002` as `NativeAlpha`.
- Optionally assert the emitted cell ids are `0..15`, rather than fake native-library poll indices.

### 2. Fix seven-segment mask mapping if required

If OasisEmulator `LedDisplayState.OnOff` is not already in editor/MAME digit bit order, add a mapper, for example:

```csharp
internal static int MapNativeLedDisplayMaskToOasisDigitMask(uint nativeMask)
```

Then call it in `PollSevenSegmentOutputs()` before emitting `MachineSegmentChangedEventArgs`.

Do not reuse the old `displayId * 16` stride logic unless the new snapshot API explicitly says `LedDisplays` still stores raw 16-cell blocks. The current struct strongly suggests each `LedDisplayState` is already one display/digit with its own `OnOff` mask.

### 3. Update seven-segment tests

Add tests that encode the actual new snapshot semantics directly:

- Create `System6NativeLedDisplayState { OnOff = ... }` at display index `2` and/or `5`.
- Assert the emitted `SegmentChanged` masks after mapping.
- If native and Oasis bit orders differ, include a test proving the remap with known segment values.

Avoid setting fake `SevenSegmentCells[display * 16 + bit]` unless the fake is explicitly modelling the old legacy API.

## Acceptance criteria

- `OasisEditor.Tests.System6NativeBackendTests.StartAsyncOneRunOnlyPollsAlphaSegmentsAndPublishesNativeAlphaSegmentMasks` passes with expectations aligned to the snapshot API.
- Existing seven-segment tests either pass or are updated to model `LedDisplayState.OnOff` directly.
- Real native System 6 seven-segment displays render correctly and are no longer scrambled.
- Any native-to-Oasis seven-segment mask mapping is isolated in `System6NativeBackend` or a small mapper class with tests.
- Old 16-cell stride assumptions are removed or clearly marked legacy-only.

## Codex kickoff prompt

```text
We are working in `WindowsNetProjects/OasisEditor` in `johnparker007/Oasis`.

The new OasisEmulator DLL and snapshot API are working, including audio, but one alpha/seven-seg-related test is failing and real seven-segment displays look scrambled.

Read `WindowsNetProjects/OasisEditor/Docs/Emulation/TASK_SYSTEM6_SEVEN_SEGMENT_SNAPSHOT_MAPPING_FIX.md` first.

Investigate the latest `johnparker007/OasisEmulator` output snapshot API/header/source and determine exactly what `LedDisplays[index].OnOff` contains: display indexing, bit ordering, decimal point bits, and whether it is already an Oasis/MAME digit mask or needs native-to-Oasis remapping.

Then update the editor and tests:

- Fix the stale alpha test so it matches the snapshot API. The backend now reads alpha segments from `GetOutputSnapshot()` rather than calling `GetAlphaSegments(0..15)`.
- Fix seven-segment mask/index mapping in `System6NativeBackend` if the snapshot `LedDisplayState.OnOff` bit order differs from the editor runtime's expected digit mask order.
- Add/update tests for the new snapshot semantics and any required native-to-Oasis seven-segment mask mapper.
- Avoid reintroducing the old `displayId * 16` cell-stride logic unless OasisEmulator explicitly still uses that representation.
- Run the relevant tests and verify the real System 6 test machine seven-segment display no longer appears scrambled.
```
