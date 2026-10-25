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
Multi-machine arcade loading, authored profile defaults and fruit gameplay remain
future milestones; this is not a completed arcade.

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

The first preview uses an explicit launch override, `--interaction buttons|pool`.
Next, move the default profile and its typed parameters into Machine authoring and
generated runtime contracts together, with validation, Inspector/MCP commands and a
format revision. An arcade placement can then supply a documented override. Do not
infer a profile from asset names, runtime kind or available anchors.

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
2. **Arcade composition.** Add a small current-format layout asset: room dimensions,
   visitor spawn, stable placement IDs, explicit generated Machine build dependencies,
   position/yaw and clearances. Add loader overloads taking an explicit placement root;
   remove the single-global-MachineSpawn assumption from multi-machine loading.
   Own one loader/session per placement and dispose them all. Start with a hand-authored
   two-machine layout; defer dragging furniture and decoration. Reject overlap/outside
   room/spawn-in-machine and partial-load failures with placement context. Test two
   simultaneous machines, input isolation, visitor collision and unload/reload.
3. **Basic fruit-machine loop.** First identify one usable Cabinet/Face/reel build
   and its logical coin/start/hold inputs. Current Player inspection found runtime
   metadata and dynamic device rendering, but no executing emulation bridge. Implement
   a clearly labelled Oasis demonstration with credits, stake, spin, reel stop, modest
   deterministic payout and lamps/displays using the portable device host boundary.
   Keep credits as game state, not real-money accounting. This unlocks walking up,
   inserting credit, playing and leaving. It is not proof of ROM compatibility.
4. **ROM-backed fruit machine.** Select one already-supported emulator core and a
   lawful local ROM fixture. Connect start/stop, logical inputs, reel/lamp/display
   updates and fault reporting to the same runtime state boundary. Verify a real boot,
   credit and spin. Never attach Oasis behavior to an Emulation Machine. If no viable
   core is available, report the concrete dependency and retain the labelled demo.
5. **Editor/MCP authoring and polish.** Expose profile defaults and simple arcade layout
   creation/save/build through existing document commands, then implement a designer
   over that contract. Add pool appearance, shot aiming feedback, sound, camera
   obstruction, input rebinding/controller support and fuller rules incrementally.

## Acceptance and limits

The overall milestone is complete when the visitor can walk between two machines,
enter either, take a Pool shot or play a fruit-machine credit/spin loop, leave without
stuck keys, open settings and reload without duplicated sessions. Mark the scripted
demo and ROM-backed result separately. A successful single-machine preview is only
milestone 1, not a completed multi-machine arcade.

Automated verification covers actual GLB loading/physics, bounds, CharacterController
wall contact, lifecycle and input routing. Mouse feel, camera composition, HUD layout
and rendered gameplay still need a short manual desktop session when the user permits.
Do not claim those from batch physics checks.
