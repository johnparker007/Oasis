# Pool collection/reset sample — A8.5

Canonical reusable gameplay: [behavior.oasis](behavior.oasis). This checkout contains
no authored Pool Machine, PoolTable Cabinet/GLB or PoolBall Object3D packages.
No user-local asset was changed. The integration fixture is synthetic test content,
not a playable Pool asset. Connect the sample to your actual Machine as below.

## Exact reference composition

All IDs are case-sensitive raw Machine identities, not GameObject display names.
The six pocket IDs below are a **reference contract**, not observed asset names.
Adapt typed literals to your existing semantic IDs where practical.

| Domain | Required IDs | Authoring source |
| --- | --- | --- |
| Object3D instances | `cueBall`, `ball01` … `ball15` (zero padded) | Machine instance rows, each referencing your cue/numbered Object3D asset |
| Cabinet triggers | `PocketLeftCorner`, `PocketLeftMiddle`, `PocketLeftFarCorner`, `PocketRightCorner`, `PocketRightMiddle`, `PocketRightFarCorner` | Cabinet GLB semantic nodes/meshes `OasisTrigger_<id>`; winning node name takes precedence over mesh name |
| Rack anchors | `rackBall01` … `rackBall15` | Machine anchor rows; position and XYZ Euler degrees in Machine space |
| Tray anchors | `traySlot01` … `traySlot15` | Machine anchor rows, spaced for the physical ball size in collection order |
| Cue start anchor | `rackCueBall` | Machine anchor row |
| Logical inputs | `rerack`, `newGame` | Machine input declarations |

Use the existing Project/Library asset reference picker for Cabinet and Object3D
references. Authored packages belong under `Assets/`; reference their actual
manifests, not guessed paths. Instance IDs differ from reusable Object3D asset GUIDs.
All sixteen ball definitions need working colliders and enabled dynamic root
Rigidbodies. Keep intrinsic model scale/up-axis in the Object3D definition. The
Machine owns placement; the live root/Rigidbody owns physical motion.

Place rack/cue anchors above the table collision surface with non-overlapping ball
spacing. Keep rack/cue/tray positions outside pocket trigger volumes. The tray must
support the still-active dynamic balls (e.g. solid shelf or suitable containment),
with no pocket-trigger overlap: the script clears velocity on collection, but does
not freeze physics. Collected flags suppress future pocket callbacks until reset.
This stage does not author coordinates because the real table geometry is absent.

## Behaviour

Startup, rerack and newGame activate and place all sixteen balls at authored rack/
cue anchors and clear both velocities, flags and tray occupancy. For this milestone
newGame intentionally shares rerack's physical/state outcome: there is no score or
rules state. Reload creates a fresh A8.4 session.

One bound trigger/object handler explicitly admits only the six pockets. Numbered
balls take consecutive tray slots in collection order. A fixed immutable Bool list
is replaced as a whole to record each collected ball. Duplicate/overlapping pocket
callbacks cannot consume slots or move collected balls; the explicit `< 15` guard
also protects the tray index. A cue scratch only activates/returns/zeros the cue
ball. Unrelated triggers/objects and trigger exits have no effect.

No Pool-specific C#, new DSL features, timers, schema changes, scoring, full rules,
aiming, cue animation, shot controls or multiplayer are introduced. Commands use A7;
Emulation and A8.4 fault policy retain their established behaviour.

## Connect, save and build

1. Open your actual Pool Machine in Editor. Select Runtime **Oasis**, add Oasis
   Script Behaviour, and replace its source with this canonical file. The authored
   package stores `behavior.oasis` next to its Machine manifest; do not put it in
   the generated `behavior/` directory. Keep unrelated authored data intact.
2. Assign the actual Cabinet and sixteen Object3D references; declare/match the IDs
   above and all 31 anchors and two inputs. If you retain different IDs, edit the
   typed literals, not runtime code. Do not construct references from strings.
3. Check the script diagnostics. Core compilation and Machine-aware validation must
   both succeed, including Cabinet GLB trigger discovery. Save, then build the
   Machine. Build revalidates persisted source and exports it to generated
   `behavior/behavior.oasis` with Machine runtime schema 9. Do not commit that build.

## Development keys in Player

Editor/development preview adds `RuntimeInputDevelopmentControls` to its session
root beside the existing development controls. After loading, select that root in
the Unity hierarchy and expand **Bindings** in the Inspector. Add `R → rerack` and
`N → newGame`. Focus Player's Game view. These are manual runtime Inspector settings
and must be reapplied after reload; this aid deliberately adds no serialized Machine
format or input-authoring UI. Empty bindings do nothing. Incomplete/undeclared IDs
are ignored. The component is excluded from release builds.

The bridge polls held keys and calls `RuntimeMachine.SetInputState` only on actual
transitions. Multiple keys for one input are aggregated; disable, focus loss and
reconfiguration release held inputs, while unloading drops the old Machine. Use
this bridge as the sole development keyboard owner of its bound logical inputs.

## Local validation checklist

- Run all `Oasis.Scripting.Tests` and `OasisEditor.Tests` using Windows/.NET 9;
  run all Player EditMode tests using Unity 6000.0.47f1. New Pool tests read the
  committed script (the .NET projects copy it as linked test content; Unity reads
  it from the repository). No alternate Pool program is maintained in tests.
- Connect/validate/save/build the actual Machine as above, load in Player and
  confirm all balls rack with no residual linear/angular motion or script error.
- Roll/drop numbered balls into each pocket. Check consecutive visible tray order,
  overlapping callbacks and all fifteen balls. Scratch the cue repeatedly; check
  its return with the numbered tray untouched.
- Press/release R and N after partial and full collection. Check all sixteen balls
  reset, and an already collected ball can now take the first tray slot.
- Reload, reconfigure development keys, and repeat collection to verify fresh state.
- Load an Emulation Machine and confirm normal loading with no script session.

Added automated coverage includes compiler/reference validation, pure interpreter
outcomes and live A7 roots/Rigidbodies/trigger relays, logical reset transitions,
unload/reload and development bridge transitions/lifecycle. These suites and the
actual asset/manual checklist have **not been executed in Codex**: repository
AGENTS.md forbids builds and test execution here. Static review and diff checks do
not establish a passing .NET or Unity suite. A8.6 fruit-device commands and A8.7
Whac-A-Mole validation remain separate work.
