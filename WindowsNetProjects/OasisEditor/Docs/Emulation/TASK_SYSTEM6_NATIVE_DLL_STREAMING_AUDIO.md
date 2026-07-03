# Task: Add basic streaming audio playback for the native System 6 DLL

## Context

The editor lives under `WindowsNetProjects/OasisEditor` in `johnparker007/Oasis`.

The native System 6 emulator DLL source lives in:

- `https://github.com/johnparker007/OasisEmulator`

The DLL previously played sound itself. The new direction is that the DLL should be a purer emulation core: it should expose an audio stream and leave playback to the consuming host application.

Consumers:

- Oasis Editor now: Windows/.NET desktop application.
- Oasis Player later: Unity application, likely using Unity audio callbacks rather than the Editor playback implementation.

This task is to add basic Editor-side playback of the DLL audio stream while emulation is running, with minimal dependencies and without leaking Editor audio dependencies into the generic backend abstraction.

## Current native backend files to review

- `OasisEditor/Emulation/EmulationBackendAbstractions.cs`
- `OasisEditor/Emulation/Native/System6NativeBackend.cs`
- `OasisEditor/Emulation/Native/System6NativeLibrary.cs`
- tests under `OasisEditor.Tests`
- project files under `OasisEditor/` and `OasisEditor.Tests/`

There is already a native DLL wrapper/backend. The audio work should extend that native integration rather than creating a separate emulator path.

## Required first step

Inspect the latest `johnparker007/OasisEmulator` source and identify its current audio streaming ABI.

Look for:

- exported audio functions
- audio structs / buffer descriptors
- sample rate, channel count and sample format
- whether output is pull-based or callback-based
- whether the host must call an audio pump after emulation ticks
- buffer lifetime rules
- threading requirements
- whether audio is interleaved PCM
- whether samples are `int16`, `float`, mono/stereo, fixed or configurable

If the audio ABI is not obvious, inspect the built DLL exports locally in the Codex environment if available, or ask the repo/source of truth. Do not guess delegate signatures.

Create/update a short implementation note in the PR summary mapping:

| Area | New DLL API finding | Editor implementation |
| --- | --- | --- |
| format | sample rate/channels/sample type | Wave format used by playback sink |
| buffering | pull/callback/batched frames | how `System6NativeBackend` feeds audio |
| lifecycle | init/start/reset/shutdown audio rules | when sink starts/stops/clears |
| threading | required native call thread | which loop pulls/pushes audio |
| underrun/overflow | expected behavior | buffer policy |

## Recommended Editor playback approach

Use NAudio + WASAPI shared mode for the Editor.

Reasoning:

- Plain .NET does not provide a good modern low-latency streaming PCM playback API.
- DirectX/XAudio2 is more complex than needed for the Editor.
- NAudio is a small Windows-focused .NET dependency and gives a straightforward `BufferedWaveProvider` + `WasapiOut` implementation.
- Keep this dependency in the Editor project only, not in shared emulation abstractions that Unity would later consume.

Target first-pass playback format, unless the DLL says otherwise:

- PCM signed 16-bit little-endian
- 48 kHz
- mono or stereo according to DLL output
- interleaved if stereo

If the DLL exposes a different fixed format, use that exact format. Avoid resampling in this task unless it is required for basic playback.

## Architecture goal

Add a small Editor-side audio sink abstraction so the native backend does not directly depend on NAudio types.

Suggested shape:

```csharp
public readonly record struct EmulationAudioFormat(
    int SampleRate,
    int Channels,
    int BitsPerSample);

public interface IEmulationAudioSink : IDisposable
{
    void Start(EmulationAudioFormat format);
    void PushPcm(ReadOnlySpan<byte> pcmBytes);
    void Stop();
    void Clear();
}
```

Then implement something like:

```csharp
public sealed class NAudioEmulationAudioSink : IEmulationAudioSink
{
    // Uses BufferedWaveProvider + WasapiOut.
}
```

Keep the interface in an Editor/backend appropriate namespace. Do not add NAudio types to `IEmulationBackend` or view models.

## Integration behavior

1. On native backend start:
   - discover or request the DLL audio format
   - create/start the audio sink when audio is available
   - clear any stale audio buffer after reset/start

2. While emulation is running:
   - pull or receive PCM frames from the DLL
   - push PCM bytes to `IEmulationAudioSink`
   - keep audio polling/pumping on a safe cadence matched to the DLL API
   - avoid blocking the emulation loop on audio playback

3. On pause:
   - either stop pushing audio and pause/stop the sink, or let silence/underrun occur cleanly
   - do not let a backlog of stale audio play after resume
   - clearing buffer on pause/resume is acceptable for first pass

4. On reset:
   - clear buffered audio
   - continue playback after new audio arrives

5. On stop/dispose:
   - stop and dispose audio playback cleanly
   - release any native audio buffers according to the DLL ABI
   - ensure stop/start cycles do not leak device handles

## Buffering policy

For a simple first pass:

- target around 100-250 ms of audio buffer
- discard on overflow to avoid runaway latency
- clear buffer on backend stop/reset/pause transitions
- do not crash on underrun; silence or brief gaps are acceptable

If the DLL returns very small audio blocks, batch them before calling `PushPcm` only if needed.

## Dependency guidance

Add the smallest suitable NAudio package reference to the Editor project. Prefer current NAudio package usage already compatible with the project target framework.

Do not add NAudio to:

- core shared model projects, if any
- tests unless directly needed
- the future Unity-facing abstraction

If NAudio causes project/TFM issues, implement a tiny fallback behind the same `IEmulationAudioSink` interface and document why.

## Native wrapper changes

Update `System6NativeLibrary` to bind the new audio exports exactly.

Possible API shapes to support after inspecting OasisEmulator:

- pull audio into a host-provided buffer
- return pointer + byte count to an internal buffer
- callback registration from native to managed
- batched machine state struct containing audio bytes/frame count

Prefer a pull or batched-state design over native-to-managed callbacks if the DLL offers both, because it is easier to reason about lifetime/threading in the Editor.

For pointer-return APIs:

- copy bytes into managed memory before the native buffer can be reused
- avoid keeping `Span`/pointer references beyond the native call lifetime

For host-provided-buffer APIs:

- use a reusable managed byte array pinned only for the native call, or stackalloc only for small fixed blocks
- validate byte counts returned by native code

For callback APIs:

- keep delegate instances rooted for the lifetime of the DLL
- marshal quickly into a lock-free or bounded queue
- do not call NAudio directly from the native callback unless the API explicitly allows it

## Tests

Add/update tests for:

- audio sink receives Start with the expected format when native audio is available
- PCM bytes from the fake native library are pushed to the sink while running
- pause/reset/stop clear or stop the sink as intended
- backend still works when audio exports are absent or disabled, if compatibility is required
- malformed native audio format fails with a clear error
- no stale audio is pushed after stop/dispose

Use a fake `IEmulationAudioSink` for backend tests. Do not require a real audio device in automated tests.

## Acceptance criteria

- Oasis Editor builds.
- Tests pass without needing a physical/default audio device.
- Running the native System 6 backend with the new DLL produces audible playback in the Editor.
- Audio starts when emulation starts and stops when emulation stops.
- Reset/pause/resume do not produce long stale audio backlogs.
- The native backend remains the only place that knows about the DLL audio ABI.
- NAudio is isolated to the Editor playback sink implementation.
- Unity/Oasis Player can later reuse the same conceptual audio format/PCM stream contract without taking an NAudio dependency.

## Starting Codex prompt

Use this prompt to start implementation:

```text
We are working in `WindowsNetProjects/OasisEditor` in the `johnparker007/Oasis` repo.

The native System 6 emulator DLL is now moving from playing its own sound to exposing an audio stream. The Editor should play that stream while native emulation is running. The DLL source is in `https://github.com/johnparker007/OasisEmulator`; inspect its latest source/API first and identify the audio streaming ABI before changing signatures.

Read `WindowsNetProjects/OasisEditor/Docs/Emulation/TASK_SYSTEM6_NATIVE_DLL_STREAMING_AUDIO.md` before implementing.

Implement basic Editor-side streaming audio playback with minimal dependencies. Prefer an `IEmulationAudioSink` abstraction plus an NAudio/WASAPI implementation using `BufferedWaveProvider` and `WasapiOut`, isolated to the Editor. Do not put NAudio types into `IEmulationBackend` or broader shared model code.

Update `System6NativeLibrary` to bind the new audio exports exactly, then update `System6NativeBackend` to start/stop/clear the audio sink with backend lifecycle and push PCM frames from the DLL while emulation is running. Use the DLL's actual sample format; do not guess if it differs from 48 kHz signed 16-bit PCM.

Add fake-sink tests for start, streaming, reset/pause/stop clearing behavior, absent audio support if needed, and malformed audio format handling. Ensure tests do not require a real audio device. Build and run relevant tests.
```
