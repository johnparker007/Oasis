> **Historical document:** This describes the former direct Amber API v2 integration, including `AmberGetApi`, and not the current OasisEditor runtime architecture. Current runtime access is exclusively through FabricRuntime.dll and its production `amber` backend.

# Amber Bridge v0.2 integration

## Authoritative contract

The managed ABI follows `johnparker007/AmberOasisBridge` commit
`c04ab068935549f4dc953e91562452dc4665949e`, principally
`include/amber/amber_api.h` and `include/amber/amber_types.h`, together with the
v2 capability, lifecycle and JPM adapter architecture documents named in that
commit.

Oasis makes one `AmberGetApi` call for `0x00020000` with a 144-byte table. The
80-byte v1 prefix is retained only for layout verification; there is no v1
fallback. `AmberBridgeInfo.api_version` remains compatibility product metadata
and is logged separately from the authoritative negotiated table version.

## ABI and capabilities

The managed aggregate sizes are: capabilities 32, lamp 8, alpha display 52,
seven-segment display 8, output snapshot 4592, audio format 32, reel entry 24,
reel aggregate 208, coin channel 20, coin route 32, and coin aggregate 408
bytes. Snapshot storage is one reusable blittable fixed-buffer value owned by
the backend. Meaningful counts are validated before indexed access; unused
fixed tails are never published.

Capability bits represent switch input, output snapshots, audio, reel
configuration, coin configuration and percentage switch. Interactive System 6
startup requires the first three. The maintained adapter normally reports
`0x3f` and 256 switches, but unknown bits and absent unused configuration
features are tolerated.

## Lifecycle and serialization

Lifecycle is create, capabilities, initialise, reset, audio-format validation,
project configuration, then run. All bridge calls are serialized by the bridge
wrapper lock; UI switch changes are queued and drained between completed run
slices. Each 1 kHz slice runs the core, drains switches, reads one coherent
snapshot, publishes changed component events, and fills 96 stereo audio frames.
Shutdown releases asserted switches before the instance is shut down and
destroyed.

Reset releases asserted switches, clears queued input, resets native state,
clears output comparisons and managed audio, and fetches a fresh snapshot.
Native retained configuration is not resent by ordinary reset.

## Output mapping

Snapshot indexes remain zero-based. Lamp logical state and decoded brightness
(`raw / 65536.0`) remain distinct in the bridge model; the existing editor lamp
event is emitted only for changes. Signed reel positions are passed unchanged.
The one reported alpha display publishes its 16 masks without reordering; the
punctuation bytes remain available in the semantic snapshot. Seven-segment
masks are forwarded exactly as returned, without a second bit reversal.

The publication route is Amber snapshot -> `System6NativeBackend` change events
-> `MainWindowViewModel` runtime adapters -> editor component models and visual
bindings.

## Audio

Startup requires 48000 Hz, two-channel signed PCM16 interleaved audio. Native
fills occur only on the serialized emulation path, never from the device
callback. Each request is measured in frames and backed by a reusable stereo
`short` array. PCM is pushed into the existing `IEmulationAudioSink`; its
`BufferedWaveProvider` is bounded by the configured buffer duration, clears
excess latency, drops overflow, and supplies silence on underrun. Reset clears
that managed buffer.

## Configuration and remaining limitations

Existing reel opto fields map to the reel apply mask and entries. Enabled Oasis
coin rows map independently to channel and route masks; Oasis has no explicit
aggregate lockout-port-base/value setting, so the lockout apply flag remains
clear. The existing `PercentSwitchValue` is already persisted as a raw nibble
0-15 and is applied directly. Mars serial coin pulses remain unsupported.

Runtime success with ROMs and audible/visual Windows behaviour must still be
verified manually; this document does not claim those checks were observed in
the coding environment.
