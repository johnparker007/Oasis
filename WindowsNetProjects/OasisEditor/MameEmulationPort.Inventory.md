# MAME Emulation Port Inventory (Phase A)

Scope: legacy Unity `LayoutEditor` MAME behavior inventory to guide the WPF/.NET port in `WindowsNetProjects/OasisEditor`.

## 1) Legacy preference storage (MAME executable/version/download location)

### What is explicitly stored
- `Preferences` stores `MameVersion` in Unity `PlayerPrefs` under key `MameVersion` (`kKeyMameVersion`), with default `239` and clamp `0..9999`. This is the only MAME-specific preference in that class.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Preferences.cs`

### What is derived (not directly stored)
- `MameController.MameExeDirectoryFullPath` computes install path from `Application.persistentDataPath/Downloads/MAME/mame####`, where `####` is zero-padded `Preferences.MameVersion`.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`
- `MameController` then launches `mame.exe` from that computed folder.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`

### Download/install behavior connected to preferences
- Preferences panel (`PanelEditorPreferencesMame`) lets user edit `MameVersion` and trigger installer button, which calls `MameDownloader.DownloadAndExtractAsync(Editor.Instance.Preferences.MameVersion, ...)`.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/LayoutEditor/Panels/PanelEditorPreferencesMame.cs`
- `MameDownloader` download root: `Application.persistentDataPath/Downloads/MAME`.  
- Extraction folder: `.../mame####`.  
- Archive naming: `mame####b_x64.exe` for version >= 281, else `mame####b_64bit.exe`.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Download/MameDownloader.cs`

### Gaps to preserve or clarify in port
- No explicit stored field for full executable path; legacy assumes convention-based path from selected version.
- No explicit stored download URL or source metadata in preferences (hardcoded GitHub release endpoints in downloader).

## 2) Legacy project settings for fruit machine platform type

- Project settings model contains `SettingsData.FruitMachine.Platform` typed as `MameController.PlatformType` enum.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Project/SettingsData.cs`
- Project settings UI (`PanelProjectSettings`) exposes enum dropdown with all enum values and persists selection into `Editor.Instance.Project.Settings.FruitMachine.Platform`.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/LayoutEditor/Panels/PanelProjectSettings.cs`
- Import path expects JSON field `project_settings.FruitMachine_Platform` and parses to enum name via `Enum.Parse(..., ignoreCase: true)`.  
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Import/Importer.cs`

### Platform enum inventory
Defined on `MameController.PlatformType`; includes (abbrev): `Scorpion1/2/4/5`, `MPU1/2/3/4/4Video/4Plasma/5`, `Impact`, `M1AB`, `BLACKBOX`, `SYS83`, `SYS85`, `Adder5`, `Proconn`, `AceSys1`, `AceSPACE`, `MMM`, `Epoch`, `SRU`, `Sys80`, `MPS2`, `Sys5`, `Mach2000E/A`, `IGTSPlus`, `IGTS2000`, `ACEVideo`, `Electrocoin`, `Coinmaster`, `AstraA1`, `Pluto5`, `Phoenix`, `Electro`, `M1Video`, `INDER`, `PCLMAXI`, `Phoenix2`, plus `None`.  
- `GetPlatformFromMfmeSystem(...)` contains a system-string mapping table and default fallback to `Electro` with error log.
  - Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`

## 3) Legacy MameController launch/process configuration

### Arguments assembled by `StartMame(bool loadState)`
Base executable:
- Working dir: `MameExeDirectoryFullPath`.
- Executable: `mame.exe`.

Command-line pieces (flag-controlled):
- Output mode (mutually exclusive):
  - `-output console` **or** `-output network`
- Optional: `-console`
- Optional: `-plugin oasis`
- Optional: `-skip_gameinfo`
- Video mode:
  - if `ArgsVideoNone`: `-video none -seconds_to_run 999999999`
  - else: `-window`
- ROM path always appended:
  - `-rompath "<persistentDataPath>/Downloads/MAME/ROMs"` (slashes normalized to `\`)
- Optional state load on startup:
  - `-state oasis_save_state` when `loadState=true`
- Driver/ROM selector is prepended from project settings:
  - first token = `Editor.Instance.Project.Settings.Mame.RomName`

Final launch string shape:
`<RomName> [flags above...]`

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`, `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Download/MameRomDownloader.cs`.

### Process setup
- Uses `ProcessStartInfo(execPath, arguments)`.
- `UseShellExecute = false`.
- Redirects stdin/stdout/stderr = true.
- `CreateNoWindow` controlled by `ProcessCreateNoWindow`.
- Hooks `OutputDataReceived` and `ErrorDataReceived`, starts async line reads.
- On destroy: unsubscribes output handler, cancels output read, then kills process.

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`.

### Prefab default toggles (legacy runtime defaults)
From `Prefabs/MameController.prefab`:
- `ArgsOutputConsole: 1`
- `ArgsVideoNone: 0` (so `-window` by default)
- `ForceVsyncOffWhenRunning: 1`
- `DebugOutputStdOut: 0`

Source: `UnityProjects/LayoutEditor/Assets/_Project/Prefabs/MameController.prefab`.

## 4) Stdout/stderr protocol inventory (legacy behavior)

### Parsed data prefixes from MAME output (`-output console`)
`ProcessLine(...)` recognizes:
- `lamp...` -> lamp parser
- `reel...` -> reel parser
- `vfd...` -> VFD parser
- `digit...` -> digit parser
Unknown prefixes are ignored.

Recognized-but-not-implemented/comments mention:
- `sreel`, `triac`, `text`, `lamplabel`, `vfdblank`, and pixel snapshot sentinel `pixel_data_start`.

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`.

### Data structures updated
- `LampValues[1024]`
- `ReelValues[16]`
- `VfdValues[16]`
- `VfdDuty[4]` (with current hard override `VfdDuty[0] = 31` in parser)
- `DigitValues[64]`

### Line format assumptions in parser
- Lamp/reel parse assumes `<prefix><index> ... <value>` where index is contiguous after prefix, and value is after last space.
- VFD parser:
  - `vfdduty*` sets duty (currently only stores into `VfdDuty[0]`)
  - `vfdblank*` TODO, currently ignored
  - else reads 2-char substring after `vfd` as index (fragile for 1-digit or >2-digit ids)
- Digit parser reads 2-char substring after `digit` as index; `-1` value normalized to `0`.

### Status/error/ready lines
- No explicit “ready” signal consumed in `MameController`.
- Lua plugin and MAME stderr lines are only logged when debug flag enabled; no state machine depends on them.
- `OnErrorDataReceived` prefixes logs as `[MAME-ERR]` (debug-only).

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`.

## 5) Stdin / Lua command format and escaping

### Command transport
- Controller writes plain text command lines to `Process.StandardInput.WriteLine(...)`.
- Command names used by controller:
  - `exit`
  - `soft_reset`
  - `hard_reset`
  - `pause`
  - `resume`
  - `throttled <bool>`
  - `state_load oasis_save_state`
  - `state_save oasis_save_state`
  - `state_save_and_exit oasis_save_state`
  - `snapshot_pixels` (controller-side test method; not wired in plugin command table)
  - `set_input_value <tag> <mask> <0|1>`

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`.

### Lua plugin command dispatch
- Plugin starts stdin thread via `emu.thread()` and periodically consumes command lines after `emu.register_prestart` marks session active.
- Command processor tokenizes with `utility:quoted_string_split(line)` and dispatches to module table by lowercased first token.
- Supports `?` prefix for arbitrary Lua evaluation (`load(expr)` execution).

Source: `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/system/stdin_thread.lua`, `.../system/command_processor.lua`, `.../system/utility.lua`.

### Escaping/quoting behavior constraints
- Tokenizer supports single-quoted or double-quoted tokens by scanning for matching quote; no escaping inside quoted token is handled.
- Unmatched quotes behavior is implicit/fragile (search for next matching quote only).
- Current controller does not quote arguments at all for `set_input_value` or other commands.

Implication for port: if arguments can contain spaces/quotes, a robust escaping contract is needed (currently undefined by legacy).

## 6) Platform-specific input mappings + invoked Lua scripts

### Mapping flow
- `SetButtonState(buttonNumber, state)`:
  - tag = `MameInputPortHelper.GetMamePortTag(buttonNumber, platform)`
  - mask = `MameInputPortHelper.GetMAMEPortInputMaskName(buttonNumber)`
  - sends `set_input_value <tag> <mask> <0|1>`
- `SetCoinState(state)` sends fixed `set_input_value COINS 1 <0|1>`.

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`.

### Implemented platform tag maps in helper
- `MPU4`: ports `ORANGE1`, `ORANGE2`, `BLACK1`, `BLACK2`, `AUX1`, `AUX2`, `DIL1`, `DIL2`
- `Impact`: guessed/partial set with unknowns (`???`, `J10_x`, `J9_x`, `COIN_SENSE`, `COINS`)
- `Scorpion4`: `IN-0` through `IN-31`
- All other platform types currently log error and return empty tag.

Mask map (`button % 8`): `1,2,4,8,16,32,64,128`.

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/MameInputPortHelper.cs`.

### Lua scripts invoked by command names
Under `oasis/system/commands`:
- `exit.lua`
- `pause.lua`
- `resume.lua`
- `soft_reset.lua`
- `hard_reset.lua`
- `throttled.lua`
- `state_load.lua`
- `state_save.lua`
- `state_save_and_exit.lua`
- `set_input_value.lua`
- `snapshot_pixels.lua` exists but command registration is commented out.

Source paths:
- `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/system/commands/*`
- `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/system/command_processor.lua`

## 7) Lua plugin asset path inventory (what must be staged)

Two copies exist in repo:
1. Bundled emulator tree: `UnityProjects/LayoutEditor/Emulators/MAME/mame0258/plugins/oasis/**`
2. External assets source: `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/**`

Installer/runtime usage points:
- Downloader copy source is external assets path resolved as:
  - `<ExternalAssetsDirectory>/MameLuaPlugins/oasis`
- Destination is extracted MAME folder:
  - `<extractPath>/plugins/oasis`
- Runtime launch assumes plugin by arg `-plugin oasis`.

Source: `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Download/MameDownloader.cs`, `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`.

### Minimum file set likely required (based on requires/entrypoints)
- `oasis/plugin.json`
- `oasis/init.lua`
- `oasis/system/utility.lua`
- `oasis/system/stdin_thread.lua`
- `oasis/system/command_processor.lua`
- All command modules required by command_processor:
  - `system/commands/{exit,pause,resume,soft_reset,hard_reset,throttled,state_load,state_save,state_save_and_exit,set_input_value}.lua`
- Optional/test-only today: `system/commands/snapshot_pixels.lua`

## 8) Behavior checklist to preserve in .NET port

- [ ] Project-level ROM name prepended as first launch token.
- [ ] Project-level fruit machine platform enum persisted and editable.
- [ ] Version-driven MAME install directory convention (`mame####`) unless intentionally redesigned.
- [ ] ROM path injection `-rompath "...Downloads/MAME/ROMs"`.
- [ ] Toggle-able launch flags: output mode, console, plugin, skip game info, video mode, optional startup state load.
- [ ] Redirected stdin/stdout/stderr with async line handling.
- [ ] Prefix-based parsing for lamp/reel/vfd/digit lines and state updates.
- [ ] Lua command channel for reset/pause/state/input operations.
- [ ] Input mapping from platform+button to `set_input_value` tag/mask/value.
- [ ] Plugin staging into `plugins/oasis` before launch.

## 9) Open questions for maintainer (recorded instead of guessing)

1. **Ready signal contract:** What concrete line/event should .NET use to mark MAME session “ready for input”? Legacy has no explicit ready gate.
2. **Parser format stability:** Are lamp/reel/vfd/digit line formats fixed across target MAME versions (especially index width assumptions in `digit`/`vfd` parsing)?
3. **VFD handling intent:** Should `vfdblank*` be implemented, and should `VfdDuty[0] = 31` hard override be removed in port?
4. **Input coverage target:** Should Phase H preserve legacy behavior (only MPU4/Impact/Scorpion4 maps implemented), or require full map coverage for all `PlatformType` values?
5. **Impact guessed mappings:** Are current Impact tags (`???`, `J10_x`, `J9_x`) authoritative enough to port, or should these be replaced before migration?
6. **Plugin source of truth:** Which plugin copy should become authoritative for WPF (`Emulators/.../plugins/oasis` vs `ExternalAssets/.../MameLuaPlugins/oasis`)?
7. **Snapshot command:** Should `snapshot_pixels` remain unsupported (module exists but command disabled), or be wired as a supported diagnostic feature?
8. **Executable path strategy:** In WPF, should users pick explicit `mame.exe`, or should version selection continue to imply a conventional install path (or both)?
9. **Download source policy:** Keep GitHub releases URL pattern exactly, or move to configurable metadata/source list in preferences?
10. **State file lifecycle:** `oasis_save_state` is hardcoded. Should per-project naming/location be introduced to avoid collisions?
