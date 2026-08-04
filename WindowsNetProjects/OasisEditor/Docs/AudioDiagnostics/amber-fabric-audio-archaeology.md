# Amber/Fabric audio archaeology and diagnostics

## History findings

Code-symbol history, not commit titles alone, identifies these waypoints:

1. Last known clean pre-`AmberOasisBridge` Editor path: commit `89d45c4` (`Fix System 6 audio timing and buffering`) changed `System6NativeBackend` and `NAudioEmulationAudioSink` before any `AmberOasisBridge` symbols existed. The path was direct native System 6 audio into the managed sink.
2. First `AmberOasisBridge` implementation: commit `30d92f6` (`Add Amber Bridge v0.1.1 managed interop`) introduced `AmberBridgeLibrary`, `AmberBridgeNativeTypes`, and the v0.1.1 integration document. That version had no audio API.
3. First Amber bridge audio transport: commit `efb6001` (`Complete Amber Bridge v2 interactive integration`) introduced `GetAudioFormat`/`FillAudioFrames` and connected it to `System6NativeBackend`.
4. First Fabric-backed Amber implementation: commit `d180faa` (`Add feature-gated Fabric System 6 backend`) introduced `FabricEmulationBackend`, `FabricMachineSession`, `FabricNativeExports`, `FabricNativeTypes`, and Fabric ABI layout tests.
5. PR #593-equivalent local commit: commit `906c6e2` (`Fix Fabric audio scheduling and buffering parity`) changed Fabric audio scheduling and NAudio buffering (`CalculateAudioFramesPerTick`, prebuffering, drop diagnostics, and buffer length policy).
6. PR #617 experiments are not retained: capped 50 Hz editor advancement, `DispatcherTimer`, `PeriodicTimer`, an above-normal dedicated editor-owned thread, 1 ms NAudio push rechunking, runtime WASAPI stop/clear/reprebuffer. They were scheduler/buffer experiments that did not prove the PCM corruption boundary and one made emulation run at about half speed with warbling audio.

## Current boundary contract

| Boundary | Contract |
| --- | --- |
| Amber core -> Amber public API | Native Amber owns generation. Current repo only sees this through external DLL/provider contracts. For v2 bridge docs, the public audio format is 48 kHz, 2-channel, signed PCM16, little-endian, interleaved; `FillAudioFrames` fills a caller-provided `short` array. Native lifetime ends when the call returns because the caller owns the destination. |
| Amber public API -> AmberOasisBridge | Bridge v0.2 exposes `GetAudioFormat` and `FillAudioFrames`; counts are frames, not interleaved samples or bytes. The managed caller allocates `frames * channels` `short`s. Returned frames are consumed from the native stream. Partial reads are valid. |
| AmberOasisBridge -> Fabric provider/backend | Provider is external to this repo. The required diagnostic gap is native-provider capture immediately after Amber/bridge makes PCM available to Fabric; add that in the provider DLL so this boundary is not inferred from managed data. |
| Fabric provider/backend -> Fabric C ABI | `FabricSessionReadAudio(session, short* samples, uint frames, out uint written)` uses cdecl, x64 pointer-size handles, `uint32_t` frame capacity/return count, and `short*` interleaved sample storage. `written` must be `<= frames`; zero means no frames currently available. The destination is caller-owned and valid only for the call. |
| Fabric C ABI -> managed wrapper | `FabricNativeExports.ReadAudio` declares cdecl `short* samples`, `uint frames`, `out uint written`. `FabricMachineSession.ReadAudio` validates `frameCapacity * channels` samples, pins the span, passes frames, rejects returned frames greater than capacity, and returns an `int` frame count. |
| Managed wrapper -> `FabricEmulationBackend` | The backend owns a reusable `short[]` sized to one pump tick (`ceil(sampleRate / 1000) * channels`). Only `framesWritten * channels` samples are valid after each read. Partial and zero-frame reads are supported; unread native audio must remain native-provider-owned. |
| `FabricEmulationBackend` -> `IEmulationAudioSink` | The backend passes only the valid prefix as bytes using `MemoryMarshal.AsBytes`; there is no format conversion. Counts submitted are bytes, derived from frames. |
| `IEmulationAudioSink` -> `NAudioEmulationAudioSink` -> WASAPI | Sink accepts signed PCM16 bytes and copies them into NAudio `BufferedWaveProvider`. It prebuffers once at startup. Overflow currently drops an entire incoming block before `AddSamples`, and counters report dropped blocks/bytes. WASAPI owns device scheduling after `Play`. |

## Diagnostic capture usage

Diagnostics are disabled by default. To enable managed boundary capture on Windows, set:

```powershell
$env:OASIS_AUDIO_DIAGNOSTIC_CAPTURE_DIR = 'C:\Temp\OasisAudioCapture'
$env:OASIS_AUDIO_DIAGNOSTIC_QUEUE_BLOCKS = '512'
```

Run the known looping music for at least 30 seconds, alt-tab repeatedly during the middle, then stop normally. The backend writes raw `.s16le.pcm` files and metadata for:

- `FabricManagedRead`: exact PCM frames returned by Fabric to managed code.
- `FabricBackendSubmit`: exact PCM frames submitted by `FabricEmulationBackend` to `IEmulationAudioSink`.

Add the matching native-provider capture in the Fabric Amber provider immediately after Amber/AmberOasisBridge offers PCM to Fabric, using the same format and sequence metadata. Compare the provider file with these managed files sample-for-sample around audible pop timestamps. The offline `AudioPcmComparison.Compare` helper reports first differing frame, candidate duplicate/missing runs, maximum sample delta, discontinuity candidates, total frame-count difference, and channel mismatch; discontinuities are candidates for correlation, not automatic proof of corruption.

At normal shutdown, collect the concise `Audio diagnostics ...` summaries and the NAudio stop summary. Frame accounting should reconcile; any unexplained difference is a transport defect to investigate before changing scheduling or buffer sizes.

## PR #618 usability update

The diagnostic path is now intended to be enabled from Preferences > Fabric Emulation rather than only by process environment variables. Each run creates a unique timestamped session directory under the configured capture root and reports the selected directory in the Editor Output window. The managed captures are WAV files so they can be listened to directly after a normal stop.

Current managed boundary expectations:

- `FabricManagedRead` and `FabricBackendSubmit` are expected to match; they prove whether the managed backend changes the valid Fabric read prefix before offering it to the sink.
- `NAudioAccepted` records only PCM that the NAudio sink accepted after its overflow policy. Dropped sink blocks are recorded in `sink-drops.csv` with sequence, start frame, frame count, byte count, and reason.
- `buffer-timeline.csv` is sampled at a bounded cadence and is intended to correlate crackles with underflow risk, overflow/drop, zero-frame reads, or host scheduling stalls.
- `session-summary.txt` is written at startup and refreshed during shutdown so useful diagnostics remain available even if Debug output is not visible.
