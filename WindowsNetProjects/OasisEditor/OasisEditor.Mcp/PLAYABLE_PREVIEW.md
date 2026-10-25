# Playable machine preview

The current first slice supplies a walkable single-machine room, explicit Buttons
or Pool controls and bounded physical Pool shots. It is not the multi-machine
arcade designer. The delivery sequence is in
[the playable arcade plan](../Docs/ArchitectureNext/15_PLAYABLE_ARCADE.md).

Launch a generated Machine build with a current Windows Player:

```powershell
./Start-PlayablePreview.ps1 -PlayerExecutable 'C:\path\OasisPlayer.exe' `
  -BuildRoot 'C:\path\Generated\Builds\PoolTableTest1' -Interaction pool
```

Click to capture the pointer, use WASD and mouse look, then E near the Cabinet to
enter. In Pool mode, move the mouse to orbit the aim direction, use the wheel to
adjust shot speed, then click or Space to shoot. R reracks and N starts a new game
when those logical inputs have the authored shortcuts. E leaves; Escape leaves
and releases the pointer; F10 opens/closes graphics settings. Opening settings or
losing focus releases machine controls. Closing settings requires another click
before movement/aiming resumes.

The Pool profile currently expects the calculated example's explicit cueBall
Prefab alias/live display identity and playingSurface declaration, with an upright
Machine. All balls must settle before another shot. It resolves the current cue
rather than caching a reference across new games. Aiming is an initial elevated
orbit camera: no cue mesh, aiming line, camera obstruction or full Pool rules yet.
Buttons mode routes the Machine's authored keyboard shortcuts while engaged.
Runtime kind and control profile are independent; selecting Buttons does not
supply an emulation core or fruit-machine game rules.

The generated room encloses measured Cabinet bounds with at least three metres
of surrounding floor. The live Machine stays in place. The preview scene's old
Environment and Lights roots are disabled at runtime only; source scenes are not
rewritten. Walking collision with the Cabinet uses its authored semantic colliders.
The room currently assumes horizontal ground and a single upright Cabinet.

Background verification uses Start-PoolPlayerVerification.ps1 -Mode Playable and
the existing verify_pool_player.py client. It tests the actual local Pool GLBs,
wall clearance, real CharacterController wall contact, enter/leave input release,
a physical cue impulse, motion gating and replacement-cue lookup after newGame.
Mouse feel, visual camera/HUD layout and manual walking require a user-approved
interactive session. The standalone Windows build needs its own smoke check.
