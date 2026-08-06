# Fabric Amber emulation

Oasis Editor uses one native-emulation architecture:

```text
OasisEditor -> FabricRuntime.dll -> production Amber provider DLL
```

Oasis loads and resolves exports only from `FabricRuntime.dll`. Provider paths are opaque absolute launch values passed unchanged to Fabric. Oasis never loads an Amber provider, resolves Amber exports, identifies a machine from a provider filename, implements native timing, or falls back to another emulator.

## Machines

| Project platform | Fabric backend | Fabric machine | Configuration |
|---|---|---|---|
| Impact / JPM System 6 | `amber` | `jpm-system6` | Current System 6 Fabric Amber configuration |
| Barcrest MPU5 | `amber` | `barcrest-mpu5` | Fabric Amber MPU5 configuration v1 |

The obsolete 420-byte layout is removed. Oasis now emits Fabric's supplied 404-byte MPU5 v1 structure (`FAM5`, magic `0x354D4146`) with explicit reel, coin, and machine-option section flags and per-option apply bits. It emits no blob only when the project selects none of those sections.

## Preferences and Project Settings

Application-level **Preferences** own the Fabric runtime path, separate JPM System 6 and Barcrest MPU5 provider paths, and shared audio buffer length. Each path is validated independently.

Project-level **Project Settings** own the fruit-machine platform and ROM resource paths. The generic **Platform Settings** category switches immediately from the existing JPM System 6 / Impact controls to a dedicated MPU5 view when `SelectedFruitMachinePlatform` changes. Unsupported platforms show an explicit empty state. A future platform is added as another platform settings view plus one selection mapping rather than by adding controls to the MPU5 view.

The current project schema is version 3 and only that version is supported; it serializes the current MPU5 apply flags, reels, coins, and machine options directly without a legacy reader.

The MPU5 view edits four program ROM slots, four optional sound ROM slots, eight reel entries, two reel-controller jumper profiles, six coin channels and their global communication settings, and explicitly applied DIP/stake/prize/percentage/characteriser/PIC/SEC/hopper options. Program ROM 1 is required and Fabric ROM slots must remain contiguous.

## Runtime behavior

Fabric owns provider loading, native ABI adaptation, reset, machine timing, snapshot validation and normalization, coin-call differences, and audio scheduling. Oasis advances Fabric in nanoseconds and uses the shared NAudio lifecycle.

Oasis consumes Fabric snapshot counts without padding. Current MPU5 ranges are lamps 0-319, reels 0-7, alpha displays 0-1, and segment cells 0-39. Coins use Fabric's dedicated coin input; Fabric's mechanism-zero detail is not exposed. Coin rejection does not fail the session. Specialist MPU5 test-mode helpers and the multistate status LED remain unsupported unless Fabric exposes them publicly.

## Manual validation

On Windows, configure Fabric and the relevant provider in Preferences, select the platform in Project Settings, configure ROMs, save/reopen, and start emulation. A real proprietary MPU5 provider/ROM smoke test must verify startup, reset, audio, inputs, all reported outputs, shutdown, and switching between System 6 and MPU5 projects.
