# Oasis Editor Emulation Runtime Architecture

The Fabric-backed Oasis Editor runtime is paced by one persistent background thread owned by `FabricEmulationBackend`. The thread is named `Oasis Fabric Emulation Runner`, remains alive for the active session, and owns the regular Fabric session calls that advance emulation, process queued inputs, read PCM, and retrieve lower-frequency machine-output snapshots.

## Timing and session ownership

The runner uses a monotonic deadline at a 1 ms emulation cadence. Each executed slice advances the deadline by one slice, so ordinary short host delays become retained timing debt rather than discarded elapsed time. The runner limits immediate catch-up batches and also limits executable slices by the audio ring's writable frame capacity so produced PCM is not overwritten or dropped during recovery.

On Windows, `FabricRunnerTimer` uses a waitable timer when available and falls back to the managed wake event if timer creation is unavailable. MMCSS registration is scoped to the runner thread through `FabricRunnerMmcssRegistration`; registration failure is non-fatal, and the registration is reverted on the same thread as the runner exits. The managed thread priority remains normal.

Lifecycle methods stay asynchronous at the public API boundary. Stop signals cancellation, wakes the runner, joins the runner thread from outside the runner, then stops audio and disposes Fabric resources. Reset and pause/resume reset scheduling baselines so the next active run starts from a fresh deadline.

## Audio pipeline

Fabric audio is read after every executed 1 ms emulation slice. PCM16 samples are written into one bounded `PcmFrameRingBuffer` using frame-based writes with explicit channel count. The producer never blocks on the audio consumer; if a defensive overflow occurs, unread audio is not overwritten and rejected frames are counted.

NAudio pulls from the same ring through `PcmRingWaveProvider`. The provider copies complete PCM frames into the device buffer and fills genuine runtime underruns with silence rather than replaying stale samples or restarting `WasapiOut`. Playback starts once the startup prebuffer threshold is reached. The current low-latency defaults remain a 50 ms PCM reserve, approximately 38 ms startup prebuffer, and 25 ms WASAPI latency.

## Visual output pipeline

The 1 kHz audio/emulation path does not retrieve and publish full machine snapshots every slice. Instead, the runner samples the latest native machine-output snapshot at a lower cadence (`VisualSnapshotCadenceSlices`) and after catch-up batches. Snapshot publication is separate from audio production.

Machine output delivery to WPF is bounded by `CoalescedMachineOutputDispatcher`. Pending lamp, reel, segment, and VFD values are keyed by output identity, repeated writes collapse to the latest value, and at most one UI dispatcher callback is pending. Detach clears pending state and prevents late UI application after backend teardown.

## Runtime logging

Normal startup and stop messages are informational. The startup line reports audio format, ring capacity, prebuffer threshold, output backend, and WASAPI latency. The stop summary is intentionally concise and limited to production health counters: wall/emulated time, ratio, slices, maximum catch-up batch, exceptional discarded time, Fabric audio frames, ring writes/rejections, device PCM frames, silence frames, underrun episodes, minimum ring depth, ring capacity, and the active timing/MMCSS configuration.
