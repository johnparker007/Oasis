# Playable arcade delivery plan

The next user-authorised milestone is an arcade that can be walked around, with
one playable Pool table and one basic working fruit machine. Prioritise a functional
end-to-end loop before a visual arcade designer, polished avatars or complete game rules.
Prefab variants and the mole/coin proving cases remain deferred.

## First implementation verification

The standalone foundation has 150 focused Unity tests passing. The actual local
Pool GLBs passed room wall clearance, CharacterController wall contact, held-input
release, cue impulse, moving-ball shot rejection and cue replacement after newGame.
A Windows development Player was built and reached its ready marker in a hidden
headless startup check. Its headless shader warnings do not verify rendered output.
Authored profile defaults and multi-machine loading are merged in PR #749: Machine
schema 11 / runtime 13 requires a Buttons or Pool interaction object. Pool names its
cue Prefab alias and playing surface explicitly. Inspector choices and MCP updates
use undoable document commands. The current-format arcade package (version 1)
contains explicit placement IDs/build dependencies, room dimensions and visitor spawn.

Two actual Pool tables passed independent-session and handle-scope checks, shared-key
input isolation, a rotated placement shot, unload/reload and overlap rollback. Editor
tests now pass 1,445/1,445 and the focused Unity suite 220/220. The two-Pool sample proves the
composition layer. A Windows development Player also reached the two-machine
arcade ready marker in a hidden headless startup check. Headless shader warnings
do not verify appearance; fruit gameplay, visual checks and the arcade designer remain future
milestones. This is not the completed Pool-plus-fruit arcade.

## Ownership and interaction profiles

The Player owns the visitor, walking collision, camera, pointer capture and selection
of one active machine. Walking, machine use and settings are mutually exclusive input
contexts. Leaving, losing focus or unloading releases held logical inputs. No machine
may receive another machine's controls.

Use explicit interaction profiles, separate from runtime kind. A Pool table can use
Oasis scripting; a fruit/video cabinet may use Oasis or an emulator. Profile selection
must never silently choose an emulator or replace game rules with Player heuristics.
Start with Buttons and Pool. Air hockey, racing and other profiles need their own
small adapters and acceptance examples when implemented.

Machine owns logical controls, physical game entities, collection/reset, scoring and
rules. Profile adapters translate human input into logical controls or bounded physical
actions. Pool's first adapter resolves the current cue after every new game, applies
a horizontal bounded impulse and disallows shots while balls are moving. Its initial
contract is cueBall dependency/entity alias and playingSurface geometry; it supports
upright tables only. No full Pool rules or cue animation are implied.

The Machine now owns its default profile and typed parameters. The preview may
explicitly override it with `--interaction buttons|pool`; omitting the option uses
its saved profile. Arcade mode uses each Machine's saved profile and rejects this
global override. Placement-specific overrides are deferred until needed. Never infer
a profile from asset names, runtime kind or available anchors.

## Ordered milestones

1. **Playable standalone foundation (first implementation).** Replace the preview's
   fixed small room with a disposable room fitted around actual visible/solid Cabinet
   bounds. Keep the live Machine stationary. Add CharacterController walking, mouse
   look, click-to-capture, nearby E enter/leave, Escape release and F10 settings.
   Promote authored keyboard mapping to release builds and gate it to active machine
   use. Add Pool orbit aiming, wheel power and click/Space shot. Verify with the real
   calculated Pool build, including room walls and held-input cleanup. No user scene
   edits or desktop automation are required. Cabinet walking collision continues to
   use authored semantic colliders; there is no invisible solid box filling the table.
2. **Arcade composition (implemented and verified).** A small current-format layout package contains: room dimensions,
   visitor spawn, stable placement IDs, explicit generated Machine build dependencies,
   position/yaw and clearances. Add loader overloads taking an explicit placement root;
   remove the single-global-MachineSpawn assumption from multi-machine loading.
   Own one loader/session per placement and dispose them all. Start with a hand-authored
   two-machine layout; defer dragging furniture and decoration. Reject overlap/outside
   room/spawn-in-machine and partial-load failures with placement context. Test two
   simultaneous machines, input isolation, visitor collision and unload/reload.
3. **Fabric/Amber JPM Impact integration (implemented and verified).** The user
   clarified that real emulation must use the existing Editor Fabric/Amber path,
   replacing the proposed scripted fruit demo. The Player consumes the Editor's
   exported ROM dependencies and FAB2 bytes through Fabric v4, with worker-owned
   native timing, inputs, snapshots, audio and lifecycle. A real Casino Crazy ROM
   proof passed boot/output/audio/input/reset/restart and operation alongside Pool.
   The installed provider supports one active board per process. See
   [the integration contract](../../OasisEditor.Mcp/PLAYER_FABRIC_IMPACT.md).
4. **User-configured credited play.** The user will set up a current Editor project
   with verified emulation coin and Start mappings. Until then, defer credit/spin/
   payout acceptance. The current proof uses borrowed artwork and its coin was
   rejected; do not guess controls or call it a completed fruit machine. Verify the
   user's saved configuration through Editor export and the same Player boundary.
5. **Layout authoring and polish.** Profile defaults already have Inspector/MCP
   editing. Expose arcade layout creation/save/build through document commands, then implement a designer
   over that contract. Add pool appearance, shot aiming feedback, sound, camera
   obstruction, input rebinding/controller support and fuller rules incrementally.

## Acceptance and limits

The overall milestone is complete when the visitor can walk between two machines,
enter either, take a Pool shot or play a fruit-machine credit/spin loop, leave without
stuck keys, open settings and reload without duplicated sessions. Distinguish verified native integration from actual credited-play/payout acceptance. A successful single-machine preview is only
milestone 1, not a completed multi-machine arcade.

Automated verification covers actual GLB loading/physics, bounds, CharacterController
wall contact, lifecycle and input routing. Mouse feel, camera composition, HUD layout
and rendered gameplay still need a short manual desktop session when the user permits.
Do not claim those from batch physics checks.
