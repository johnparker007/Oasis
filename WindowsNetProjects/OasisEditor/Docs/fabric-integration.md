# Fabric Amber emulation

Oasis Editor keeps the native-emulation boundary as:

```text
OasisEditor -> FabricRuntime.dll -> Amber provider DLL
```

Oasis loads only `FabricRuntime.dll`. The configured Amber provider DLL path is passed to Fabric explicitly in the launch request; Oasis never loads or calls a production Amber provider DLL directly and never substitutes another provider path.

## Supported Amber machines

Both currently supported native-emulation platforms use Fabric backend identifier `amber`:

| Oasis platform | Fabric machine identifier | Provider path |
| --- | --- | --- |
| `Impact` (JPM System 6) | `jpm-system6` | The configured System 6 Amber provider DLL path |
| `MPU5` (Barcrest MPU5) | `barcrest-mpu5` | The configured Barcrest MPU5 Amber provider DLL path |

Unsupported platforms fail explicitly. There is no MAME or alternate-runtime fallback in the Fabric startup path.

## Configuration

System 6 and MPU5 have separate Oasis settings models and separate strongly typed Fabric conversion paths. System 6 uses `System6NativeRomSettings` with `FabricAmberConfiguration.FromSystem6(...)`; MPU5 uses `Mpu5NativeRomSettings` with `FabricAmberMpu5Configuration.FromMpu5(...)`.

The current MPU5 settings model contains only fields used by the checked-in Fabric contract:

- up to four program ROM paths and four sound ROM paths;
- eight reel opto entries, including enabled state, step count, opto start/end, and inversion;
- percentage, stake, and prize values;
- sixteen DIP switch bits;
- PIC mode and characteriser address;
- SEC-fitted state;
- hopper type;
- reel jumper/profile value;
- electronic coin mechanism communication style, invert flag, and nonzero pulse timing.

Coin insertion remains routed through Fabric's dedicated coin input API. For initial MPU5 support, mechanism index zero is owned by Fabric; Oasis submits the coin channel, denomination, and active edge instead of asserting raw switch-matrix inputs.

## Output ranges

Oasis consumes Fabric-reported output counts rather than padding Amber outputs to a System 6 total. The current MPU5 range targets are:

- matrix lamps: indices `0-319`;
- reels: indices `0-7`;
- alpha displays: display indices `0-1`, each with the Fabric character capacity and display-wide brightness;
- segment display cells: indices `0-39`.

Alpha display segment masks are passed through the existing Amber alpha mapper, with dot/comma attributes retained in the high bits used by Oasis display bindings. Multiple alpha displays in a snapshot are flattened by display ordinal, so the second MPU5 alpha display is not discarded.

## Intentionally unsupported MPU5 diagnostics

The first MPU5 integration does not expose higher-level Amber diagnostic exports, direct provider-DLL calls, diagnostic-only switch simulations, or MPU5-specific audio scheduling in Oasis. Scheduler timing, audio generation, and platform-specific cycle timing remain Fabric responsibilities.
