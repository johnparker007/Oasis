# Arcade packaging and launch

The Player now loads several explicitly placed Machines into one walkable room.
Each placement owns an independent runtime session, even when two placements use
the same generated build. The sample uses two Pool tables to prove composition;
it does not contain a fruit-machine game yet.

## Machine profile

Use the current Editor (Machine authored schema 11 / generated runtime 13).
The Machine Inspector offers Buttons or Pool. Choosing Pool requires explicit
cue Prefab alias and playing surface choices; no first asset is guessed.
The MCP equivalent is `set_machine_interaction(documentId, "Pool", "cueBall",
"playingSurface")`, followed by save and build. Buttons has no Pool parameters.
Pool currently requires one live dynamic cue entity whose display identity is
the selected Prefab alias, a root Sphere collider, the selected surface and an
upright Machine. Runtime failure identifies the affected placement.

## Package explicit builds

```powershell
python package_arcade.py --output 'C:\Arcades\TwoTables' `
  --machine pool-left 'C:\Builds\PoolTableTest1' -2.5 0 90 `
  --machine pool-right 'C:\Builds\PoolTableTest1' 2.5 0 0
```

The five placement arguments are ID, generated build folder, X, Z and yaw in
degrees. Only the specified build dependencies are copied. The output must be a
new folder outside the source builds. Identical source builds are packaged once
but loaded into independent sessions. The default room is 12 by 10 metres, with
visitor spawn at (0, 0, -3); width, length and visitor Z can be overridden.

The package owns `arcade.runtime.json` (schema `oasis.arcade.runtime`, version 1),
room dimensions, visitor spawn/yaw and placement IDs, dependency paths, positions
and yaw. Dependencies must remain beneath the package root. There is no automatic
project scan, compatibility loader or runtime reparenting of existing Machines.

## Start the arcade

```powershell
./Start-Arcade.ps1 -PlayerExecutable 'C:\Player\OasisPlayer.exe' `
  -ArcadeManifest 'C:\Arcades\TwoTables\arcade.runtime.json'
```

This launches `--mode arcade --arcade <manifest>`. Arcade mode uses each Machine's
saved profile and rejects global `--interaction` and `--build` arguments. Preview
mode still accepts an explicit profile override; its default now uses the saved
Machine profile.

Click to capture the pointer; WASD walks, mouse looks and E uses the nearest
Machine within reach. E leaves, Escape leaves/releases the pointer, and F10 opens
settings. Only the engaged station receives authored keys. In Pool, mouse aims,
wheel changes power and click/Space shoots. R/N act when authored as rerack/newGame.

Actual Cabinet bounds must fit the horizontal room floor, room height and 0.75m
wall/machine clearance; the visitor's capsule must start clear of Cabinets.
Rotated placements use conservative world bounds. Invalid dependencies fail before
instantiation; measured placement failures and startup faults dispose all loaded
sessions. No Machine scripts start before the measured layout passes validation.
Walking collision uses authored Cabinet colliders; the layout does not fabricate
a solid box inside a Pool table.

## Verification

`Start-PoolPlayerVerification.ps1 -Mode Arcade -ArcadeManifest <manifest>` plus the
existing verification client exercises two actual Pool GLBs: live-handle isolation,
held-key release/switching, rotated cue shot, independent physics, unload/reload and
overlap rollback. The latest native-integration focused Unity suite passed 220 tests; Editor suites
passed 1,445. Rendered arcade layout, Inspector appearance and mouse feel still
require a permitted manual desktop session.

A separate Pool + real JPM Impact integration fixture is now available; see
[Fabric/Impact hosting](PLAYER_FABRIC_IMPACT.md). It uses explicit local native DLLs
and does not yet verify game-specific credit/Start/payout mappings.
