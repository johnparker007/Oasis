# Fabric Amber emulation

Oasis Editor has one native-emulation architecture:

```text
OasisEditor -> FabricRuntime.dll -> production Amber provider DLL
```

Oasis loads and resolves exports only from `FabricRuntime.dll`. The absolute provider path is an opaque launch value passed unchanged to Fabric; Oasis never loads the provider, resolves Amber exports, calls the Amber ABI, identifies a machine from a DLL filename, or falls back to another emulator.

## Supported machines

| Oasis platform | Fabric backend | Fabric machine | Provider preference |
|---|---|---|---|
| JPM System 6 (`Impact` project metadata) | `amber` | `jpm-system6` | JPM System 6 Amber provider DLL |
| Barcrest MPU5 (`MPU5` project metadata) | `amber` | `barcrest-mpu5` | Barcrest MPU5 Amber provider DLL |

The Fabric runtime path and the two provider paths are distinct settings and are validated independently. Program and sound ROM paths are also validated as ROM resources. No provider is substituted for another platform.

## Machine configuration

System 6 and MPU5 have separate project and Fabric configuration models. The MPU5 model supplies up to four program ROMs, four sound ROMs, eight indexed reel configurations (enable, steps, opto window/inversion and jumper profile), machine percentage/stake/prize/DIP/PIC/characteriser/SEC/hopper options, and six indexed coin channels (enable, value and lockout inversion). These fields are serialized into the current fixed-width Fabric MPU5 configuration blob; they are not Amber provider calls.

Fabric owns reset, native ABI adaptation, each machine's cycles per millisecond, snapshot count validation and normalization, coin-call differences, and audio scheduling. Oasis advances a Fabric session in nanoseconds and shares the existing NAudio buffering and shutdown lifecycle for both machines.

## Outputs and inputs

Oasis consumes the counts and numerical indices returned in each Fabric snapshot rather than manufacturing a System 6-shaped snapshot. Current normalized ranges are:

* System 6: 512 matrix lamps, eight reels, one 16-character alpha display, and 16 segment cells.
* MPU5: 320 matrix lamps (indices 0-319), eight reels (0-7), as many as two independent 16-character alpha displays (indices 0 and 1), and 40 segment cells (0-39).

Character masks, punctuation attributes and display brightness remain distinct for each alpha display. Segment cells above 15 are neither wrapped nor discarded. The MPU5 multicolour status LED is not in the current public snapshot and Oasis does not invent a boolean substitute.

Cabinet switches use Fabric digital inputs. Coins use Fabric's dedicated coin input with channel, denomination and active state, on the existing inactive-to-active input path. Fabric internally uses MPU5 mechanism zero; Oasis does not expose that native mechanism. `FABRIC_INPUT_REJECTED` means a rejected coin and does not fail the session.

Specialist native MPU5 secondary-test/test-mode helpers are deliberately unsupported because the current Fabric public ABI does not expose them. Normal service controls should use Fabric digital inputs.

## Manual validation

A proprietary provider or ROM is not committed. On Windows, configure absolute Fabric runtime, machine-specific provider and ROM paths, then manually smoke-test start/reset/audio/input/output/shutdown and switching between System 6 and MPU5 projects. Automated selection, managed serialization and native layout probes do not require the proprietary MPU5 DLL.
