# Amber backend comparison diagnostics

## Executable paths inspected

The direct path is `System6NativeBackend` (`Emulation/Native/System6NativeBackend.cs`) through
`IAmberBridgeLibrary` and `AmberBridgeLibrary` (`Emulation/Native/AmberBridge`). Its loop is a
fixed 1 kHz loop: one 8,000-cycle `Run`, input drain, snapshot, then audio fill. It clamps a stall
by discarding catch-up beyond three pump ticks.

The Fabric path is `FabricEmulationBackend` (`Emulation/Fabric/FabricEmulationBackend.cs`) through
`FabricRuntimeLibrary` and `FabricMachineSession`. Its loop supplies monotonic variable elapsed
nanoseconds to one `Advance`, then drains input, requests a snapshot, and reads audio. The elapsed
converter retains fractional nanoseconds and the loop has no Oasis-side catch-up cap.

`EmulationBackendFactory` selects Fabric when `UseFabricForAmber` is true and both Fabric paths are
configured. Otherwise Impact uses `System6NativeBackend` when AmberBridge.dll is configured, then
falls back to MAME. `MainWindowViewModel` creates the factory, starts/stops it through
`EmulationSessionController`, routes input through `PlayViewInputRouter`, and applies backend output
events to `MachineRuntimeState`. ROMs and System 6 reel/coin/percentage settings originate in
`System6NativeRomSettings`; `FabricAmberConfiguration.FromSystem6` performs Fabric request mapping.
Audio is negotiated and read in each backend and submitted through `IEmulationAudioSink` /
`NAudioEmulationAudioSink`. Each backend owns cancellation, asserted-input release, audio stop,
native shutdown, and disposal.

## Preference and transcript

`NativeEmulation.EnableAmberBackendComparisonLogging` is persisted by the existing JSON preference
store and is exposed under **Native Emulation** as **Enable Amber backend comparison logging**. It
defaults to false and is sampled whenever a backend starts, so neither Visual Studio nor Oasis needs
restarting. Disabled sessions allocate only a small inert session object and emit no pump events.

Both transports use `AmberComparisonSession` and the single-line schema:

```text
[AmberCompare] session=... backend=direct|fabric sequence=... elapsed_ns=... thread=... operation=... arguments=... result=... summary=...
```

Every start has a fresh eight-character id and explicit `ComparisonStart` / `ComparisonEnd` markers.
The start includes selection, platform, machine, process and safe DLL filenames. ROM events include
role, slot, configured index, basename and presence, never bytes. Configuration events preserve raw
values. Bounds reset per session: 20 `Advance`, 8 `GetSnapshot`, 16 `ReadAudio`, and 32 queued input
events. Lifecycle and shutdown events are not bounded. Fabric correctly reports
`native_return:unavailable` because the public ABI exposes no native Amber Run result.

## Earliest source-level disparity and A/B procedure

No proprietary game, ROM set, Amber DLL, Windows audio device, or Windows WPF runtime is available
in the Codex environment, so paired behavioural transcripts cannot honestly be manufactured. The
earliest observable source-level difference is execution cadence:

```text
direct: Advance elapsed_ns:1000000 time_source:fixed requested_cycles:8000 native_run_calls:1 maximum_catch_up:3 clamping:true
fabric: Advance elapsed_ns:<measured> time_source:monotonic_variable advance_calls:1 native_return:unavailable catch_up:false clamping:false
```

An earlier lifecycle difference is also intentionally exposed: direct calls `Initialise`, explicit
startup `Reset`, audio negotiation, then configuration; Oasis passes Fabric ROM/configuration in
`CreateSession`, calls `Initialise`, and does not issue a speculative second startup reset. Whether
the production adapter resets or reapplies configuration inside those calls is not observable from
the public Fabric ABI. Changing cadence or adding a reset without paired evidence could hide the
actual first defect, so this change deliberately instruments rather than guesses.

For validation, enable the preference, run Direct Amber for at least 20 iterations / 8 snapshots /
16 audio reads, stop, change the existing Fabric checkbox, and repeat without restarting. Align the
two session blocks first at lifecycle, then ROM/configuration, then `Advance`, snapshot and audio.

## Conditional native follow-up specification

If matched Oasis request metadata followed by the first Fabric `Advance` remains static while the
direct `Run(8000)` changes output or produces audio, add a bounded diagnostic callback to the
production Amber adapter in `AmberOasisBridge`: emit, for the first 20 Fabric advances, elapsed ns,
cycle budget, number of Amber `Run` calls, each requested cycle count, each signed returned cycle
count, accumulated remainder, and clamp/discard decision. Also emit adapter call order for create,
ROM-slot marshal, initialise, reset, reel/coin/percentage application, snapshot and audio read.

Tests in that separate repository must prove: (1) 1 ms advances have cadence equivalent to one
direct `Run(8000)` or document the production core's required contract; (2) delayed advances follow
the chosen cap/remainder policy; (3) program and sound resources retain sparse slot identity; (4)
configuration survives the adapter's reset order; (5) advancing yields changing snapshots and
nonzero audio with a deterministic fake production Amber DLL. Acceptance requires paired transcripts
to match through the first native Run, or to show the exact native call/return that diverges. No file
under `johnparker007/AmberOasisBridge` is changed by this work.
