# MAME Emulation Runtime Inventory (Step 1)

Date: 2026-05-11
Scope: Legacy Unity `LayoutEditor` emulation runtime behavior that must be understood before WPF runtime code changes.

## 1) Legacy Emulation menu inventory

Legacy native menu defines these `Emulation/*` entries:

- `Start And Load State`
- `Save State And Exit`
- `Start`
- `Load State`
- `Save State`
- `Exit`
- `Pause`
- `Resume`
- `Throttle`
- `Unthrottle`
- `Soft Reset`
- `Hard Reset`

Dispatch behavior is thin: handlers call `Editor.Instance.MameController` methods directly (e.g., `StartMame`, `Pause`, `Resume`, `Exit`, reset/state commands).

Implication for WPF port: current plan’s `Start/Stop/Pause/Resume` shell is a subset of legacy and should remain command-driven, with room for later expansion to reset/throttle/state commands.

## 2) Legacy process launch args and runtime toggles

`MameController.StartMame(bool loadState)` constructs args in this shape:

- First token: project ROM name (`Project.Settings.Mame.RomName`)
- Output mode: either `-output console` or `-output network`
- Optional toggles:
  - `-console`
  - `-plugin oasis`
  - `-skip_gameinfo`
- Video mode:
  - `-video none -seconds_to_run 999999999` when `ArgsVideoNone`
  - otherwise `-window`
- ROM path always appended:
  - `-rompath "<persistentDataPath>\\Downloads\\MAME\\ROMs"`
- Optional save-state on start:
  - `-state oasis_save_state` when `loadState == true`

Process configuration:

- Working directory: computed MAME version folder (`.../Downloads/MAME/mame####`)
- Executable: `mame.exe`
- `UseShellExecute = false`
- `RedirectStandardInput/Output/Error = true`
- `CreateNoWindow` controlled by serialized `ProcessCreateNoWindow`
- Asynchronous output/error reads via `BeginOutputReadLine` and `BeginErrorReadLine`

## 3) Legacy Lua plugin/stdin protocol shape

Controller writes plain command lines to MAME stdin (`Process.StandardInput.WriteLine(...)`).

Observed command strings from controller:

- `exit`
- `pause`
- `resume`
- `soft_reset`
- `hard_reset`
- `throttled <bool>`
- `state_load oasis_save_state`
- `state_save oasis_save_state`
- `state_save_and_exit oasis_save_state`
- `set_input_value <tag> <mask> <0|1>`

Lua plugin side (`stdin_thread.lua` + `command_processor.lua`):

- Spins command polling thread and dispatches by lowercased first token.
- Tokenization uses quoted split helper with simple quote-pair behavior (no robust escaping semantics).
- Supports `?`-prefixed expression evaluation path.

Port implication: keep an explicit stdin writer abstraction now, but preserve plaintext line command compatibility with this plugin contract.

## 4) Legacy stdout/stderr protocol with lamp focus

`MameController.ProcessLine(...)` routes output by prefixes:

- `lamp*` -> lamp parser
- `reel*` -> reel parser
- `vfd*` -> vfd parser
- `digit*` -> digit parser

For Step 1 milestone focus, lamp line parsing behavior is:

- Parses lamp index from text between `lamp` prefix and first space.
- Parses lamp value from text after last space.
- Writes value into `LampValues[lampNumber]`.

Notable parser fragility to preserve/repair in WPF parser design:

- No guard rails around malformed lines (`int.Parse` assumptions).
- Partial/unknown lines are effectively ignored unless debug logging is enabled.
- No explicit plugin “ready” handshake used by runtime state.

Stderr handling:

- Error lines are received asynchronously and logged as `[MAME-ERR] ...` only when `DebugOutputStdOut` is enabled.

## 5) Pause / resume / stop / cleanup behavior

Legacy command behavior:

- Pause -> writes `pause`
- Resume -> writes `resume`
- Exit -> writes `exit`
- Save-and-exit -> writes `state_save_and_exit oasis_save_state`

Cleanup behavior today is split/incomplete:

- `OnDestroy()` unsubscribes output handler, cancels output read, and kills process if present.
- In-code TODO comment explicitly says this should move into a real `StopMame()` flow.
- There is no robust state machine (`Stopped/Starting/Running/...`) in legacy.

Port implication: WPF runtime should formalize explicit lifecycle states and deterministic stop/cleanup paths (including project close/editor exit).

## 6) Open runtime questions captured before implementation

1. Should WPF `Stop` map to stdin `exit`, forced kill fallback, or both with timeout?
2. Should we keep `-output console` as default (legacy prefab indicates yes), and expose network mode later only?
3. Do we treat plugin readiness as inferred from first known protocol line, or define a new ready signal contract?
4. Should stderr always be logged in WPF output log (recommended) instead of debug-gated behavior?
5. Should the initial WPF parser support lamp-only first while passing through unknown lines diagnostically (recommended for milestone)?

## 7) Files inspected for this inventory

- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/NativeMenu/NativeMenuDefinition.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/NativeMenus/SelectionHandler.Emulation.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Prefabs/MameController.prefab`
- `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/system/stdin_thread.lua`
- `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/system/command_processor.lua`
- `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/system/utility.lua`
