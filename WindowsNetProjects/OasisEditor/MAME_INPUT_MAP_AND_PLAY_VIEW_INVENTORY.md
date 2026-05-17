# MAME Input Map and Play View - Legacy Input Inventory (Step 1)

This inventory captures the legacy Unity behavior that must be preserved/ported before major runtime implementation.

## Sources inspected

- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/ExtractImporter.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/ShortcutKeyHelper.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/MameInputPortHelper.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`

## Legacy MFME import behavior

### Platform and ROM import

`ExtractImporter.OnMFMEExtractLayoutLoaded` performs both of these immediately:

1. `ImportGamData(layout)` maps `layout.GamFile.KeyValuePairs["System"][0]` to `MameController.PlatformType` using `MameController.GetPlatformFromMfmeSystem`.
2. `ImportMameRomIdent(layout)` copies `layout.MameRomIdent` into project MAME settings.

### Lamp input import (MFME lamp components)

`ImportLamp(ExtractComponentLamp)` creates one Oasis `ComponentLamp` and imports only the first MFME lamp element (`LampElements[0]`) for visual data.

Input path:

- If `extractComponentLamp.HasButtonInput`:
  - `componentLamp.Input.Enabled = true`
  - `componentLamp.Input.CoinInput = false`
  - `componentLamp.Input.ButtonNumber = int.Parse(extractComponentLamp.ButtonNumberAsString)`
  - `componentLamp.Input.Inverted = extractComponentLamp.Inverted`
  - `componentLamp.Input.KeyCode = ShortcutKeyHelper.GetKeyCode(extractComponentLamp.Shortcut1)`
- Else if `extractComponentLamp.HasCoinInput`:
  - `componentLamp.Input.Enabled = true`
  - `componentLamp.Input.CoinInput = true`
  - `componentLamp.Input.Inverted = extractComponentLamp.Inverted`
  - `componentLamp.Input.KeyCode = ShortcutKeyHelper.GetKeyCode(extractComponentLamp.Shortcut1)`

Notes:

- `Shortcut2` is not imported.
- Tag/mask are not imported here (old commented-out TODO lines mention helper calls).
- TODO comment explicitly mentions future coin/note/effect handling still incomplete.

### MFME button import behavior

`ImportButton(ExtractComponentButton)` converts MFME buttons into lamp import flow:

- `ExtractComponentLamp extractComponentLamp = new ExtractComponentLamp(extractComponentButton);`
- then calls `ImportLamp(extractComponentLamp)`.

So Unity legacy already treated MFME buttons as lamp visuals with lamp-input metadata, matching current Oasis direction for first milestone.

### Other related input import behavior

`ImportCheckbox(ExtractComponentCheckbox)` creates `ComponentSwitch` with:

- `componentSwitch.Input.Enabled = true`
- `componentSwitch.Input.ButtonNumber = extractComponentCheckbox.Number`

No shortcut mapping or platform resolution happens at import time for checkbox path.

## Legacy shortcut mapping behavior (`ShortcutKeyHelper`)

`ShortcutKeyHelper.GetKeyCode(string)`:

- trims only trailing spaces via `TrimEnd(' ')`.
- performs exact `switch` mapping on strings to Unity `KeyCode`.
- supports:
  - `SPACE`
  - digits `0-9`
  - letters `A-Z`
  - punctuation subset: `` ` - = [ ] ; ' # \ , . / ``
  - modifiers: `SHIFT`, `CTRL`, `ALT` (left-side only)
  - arrows: `UP`, `DOWN`, `LEFT`, `RIGHT`
- unsupported values return `KeyCode.None` silently.

Gaps to preserve/document during port:

- no F-keys, Escape, Insert/Delete, numpad mapping.
- no combined key expressions (`Shift+3`, etc.).
- no explicit logging for unsupported values.

## Legacy platform tag/mask mapping behavior (`MameInputPortHelper`)

### Public API

- `GetMamePortTag(int mfmeButtonNumber, PlatformType platformType)` supports only:
  - `MPU4`
  - `Impact`
  - `Scorpion4`
- other platforms log error and return empty string.

### Tag mapping shape

`kBitsPerPort = 8`; tag index is `mfmeButtonNumber / 8`.

Per-platform tag arrays:

- MPU4: `ORANGE1`, `ORANGE2`, `BLACK1`, `BLACK2`, `AUX1`, `AUX2`, `DIL1`, `DIL2`
- Impact: includes guessed values and `COINS` near end (`???`, `J10_0`, `J9_0`, `COIN_SENSE`, `COINS`, etc.)
- Scorpion4: `IN-0` through `IN-31`

No bounds checks are present for large/negative button numbers.

### Mask mapping

`GetMAMEPortInputMaskName(int mfmeButtonNumber)` uses `mfmeButtonNumber % 8` and returns decimal string mask from:

`1, 2, 4, 8, 16, 32, 64, 128`

(no hex formatting).

## Legacy MAME input command flow

`MameController` input path:

- `SetButtonState(buttonNumber, state)`:
  - resolves `tag` via `MameInputPortHelper.GetMamePortTag(buttonNumber, projectPlatform)`
  - resolves `mask` via `MameInputPortHelper.GetMAMEPortInputMaskName(buttonNumber)`
  - calls `SetPortValue(tag, mask, state)`
- `SetCoinState(state)`:
  - hardcoded `tag = "COINS"`, `mask = "1"`
  - calls `SetPortValue`
- `SetPortValue(tag, mask, keyDown)`:
  - returns early if process not running or stdin not writable
  - builds command exactly as:
    - `set_input_value <tag> <mask> <0|1>`
  - writes single line to MAME stdin.

This is the required baseline format for Oasis input command service.

## Platform mapping from MFME system string

`MameController.GetPlatformFromMfmeSystem(string)` maps many MFME system strings to enum values.

Important observation for current input work:

- input tag resolution helper supports only 3 platforms (`MPU4`, `Impact`, `Scorpion4`), while platform import maps many more.
- therefore unresolved-platform behavior is already possible in legacy and should be explicit in Oasis diagnostics.

## Porting implications / open questions

1. **InputDefinition source fields** should retain raw imported values:
   - raw shortcut string (`Shortcut1`) and maybe `Shortcut2` for future,
   - raw `ButtonNumberAsString` if parse fails,
   - imported `Inverted`, `HasButtonInput`, `HasCoinInput` semantics.
2. **Resolver abstraction is required** because legacy helper only partially supports platforms and has guessed tags.
3. **Diagnostics must improve** over legacy silent fallbacks (`KeyCode.None`, empty tag, etc.).
4. **Coin behavior** should preserve initial hardcoded command target (`COINS` / `1`) unless deliberate platform-specific override is introduced.
5. **Safety behavior** (do not send when process/stdin unavailable) must be preserved.
6. **Button-number bounds validation** should be added during port; legacy code risks out-of-range indexing.

## Step 1 completion

Inventory captured before major runtime implementation, per `MAME_INPUT_MAP_AND_PLAY_VIEW_PLAN.md` Step 1.
