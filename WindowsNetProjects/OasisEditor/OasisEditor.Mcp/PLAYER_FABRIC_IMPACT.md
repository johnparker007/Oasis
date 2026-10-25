# Fabric and Amber JPM Impact in Oasis Player

The Windows x64 Player now runs the existing Fabric v4 native boundary with the
Amber `jpm-system6` provider. This is real emulation, separate from Oasis scripting.
It replaces synthetic lamp/reel diagnostics for Emulation Machines. Other native
platforms are not implemented in the Player yet and report an unsupported-platform
error when started. Platform None remains an unconfigured preview, not emulation.

## Export and launch

Configure a current Machine as Emulation / Impact in the Editor, using its existing
System 6 ROM, reel, coin and percentage settings, then build it. Machine authored
schema remains 11; generated runtime is now 13. Only the latest runtime format is
supported, so earlier generated packages must be rebuilt.

The build copies explicit contiguous program/sound ROM slots and writes the exact
648-byte FAB2 v2 block produced by the Editor's existing configuration factory.
Its `runtime.fabric` descriptor names `amber`, `jpm-system6`, the configuration and
contained relative ROM paths. Generated Impact settings metadata has source ROM
paths removed. Native libraries are host configuration and are not bundled by this
build step.

```powershell
./Start-Arcade.ps1 -PlayerExecutable 'C:\Player\OasisPlayer.exe' `
  -ArcadeManifest 'C:\Arcades\Example\arcade.runtime.json' `
  -FabricRuntimeDll 'C:\Fabric\FabricRuntime.dll' `
  -AmberImpactDll 'C:\Amber\JPMSystem6Core.dll'
```

The Player accepts `--fabric-runtime` and `--amber-impact`, or the explicit
`OASIS_FABRIC_RUNTIME_DLL` / `OASIS_AMBER_IMPACT_DLL` environment variables. The
preview launcher accepts the same optional library parameters. Omitting its
interaction override uses the Machine's saved profile. Use Buttons for a fruit
machine; profiles do not choose the emulation platform or invent game rules.

The installed JPM provider has one file-static board instance. The Player rejects
a second simultaneous Impact board before calling native creation. One Impact
machine can run beside independently scripted machines such as Pool. Supporting
multiple Impact machines requires a provider with genuine independent instances
or a separately designed process boundary; duplicating Unity objects is insufficient.

## Ownership and mappings

One background worker owns native creation, initialise/reset, 1 ms advances, queued
input, bounded snapshots/audio reads and shutdown. It uses a monotonic clock and
bounded catch-up. Unity objects are updated on the main thread from the latest
immutable managed snapshot. The audio callback consumes a bounded PCM transfer
buffer and makes no native calls. Streaming AudioClip playback uses the native
sample rate/channels; headless verification drains audio without playing it.

Digital inputs use the authored numeric ButtonNumber, `oasis.switch.N`, and logical
press/release state. Coin inputs use Fabric's separate channel/denomination protocol;
InputRejected is recorded without faulting emulation. No digital coin-switch guesses
are introduced. Leaving/focus loss releases logical inputs through the existing
visitor controller. Shutdown/reset releases worker-held switches and accepted coins.

Outputs follow the Editor: lamp quantization, zero-based reel identities with Amber
direction/step normalization, System 6 alpha/segment bit mappings and Impact alpha
cell reversal. Grouped Face digits consume contiguous native cells. All 512 JPM lamp
channels are retained in CPU state; the existing byte-encoded Face artwork format
still supports logical lamp IDs 0–254. The GPU lookup width is deliberately unchanged;
upper lamp visuals need a separate Face-format change. Dot-matrix rendering is not
part of this Impact integration.

The adapter in `Assets/_Project/Scripts/Fabric` ports the Editor's v4 wrapper to
Unity C# 9 and Windows exact-path DLL loading, preserving bounded buffers, caller
pointer/header checks and native ownership. It does not reimplement the emulated
machine. ABI tests pin x64 sizes/offsets, including the 88-byte input structure.

## Verification and remaining gameplay work

The real local Casino Crazy ROM set (program slots 0/1 and sound slot 0) passed a
hidden Player probe: approximately 12 seconds of native execution, 756 snapshots,
512 lamps, eight reels, one alpha, sixteen segment entries and 48 kHz stereo PCM.
Service-door press/release and a dedicated coin call reached native code. The coin
was rejected by the current routing configuration; this is not proof of credited play.
Reset, singleton rejection, stop/dispose and fresh reload passed. A mixed arcade
also passed input isolation and continued native execution while the Pool cue moved.

The disposable visual assembly uses the old Bonanza Cabinet/Face/reel artwork with
Casino Crazy ROMs; it is an integration proof, not a completed game package. The
original old fruit-machine projects were not modified. The user will create a current
Editor project with verified emulation/coin/Start mappings later, at which point test
actual credit, spin and payout. Do not replace that test with scripted fruit gameplay.

Managed verification: 1,445 Editor tests and 220 focused Unity tests passed. The
earlier unrelated full Unity geometry/rendering failures are not claimed resolved.
Rendered appearance and audible playback still require a permitted manual session.
`Start-PoolPlayerVerification.ps1 -Mode Impact` accepts the two native DLL paths;
passing `-ArcadeManifest` additionally tests the mixed arcade. Verification uses real
local resources; ROMs/native binaries are never committed to this repository.
