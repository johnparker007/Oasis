# Single-ball Pool runtime proving case — A8.5

Canonical gameplay: [behavior.oasis](behavior.oasis). This initial proving case uses
one ball, two Machine-space anchors, six coordinate-based pocket trigger IDs and two logical
inputs. There are no lists, loops, tray counters, multi-ball allocation or cue-ball
rules in the script; its only mutable state is `collected`, a Bool.

No authored Pool Machine, PoolTable Cabinet/GLB or PoolBall Object3D packages were
available when this PR was created. No user-local Machine or asset was modified.
The integration fixture is synthetic test content, not a completed playable asset.
Connect this file to your actual composition; do not infer asset paths from this guide.

## Exact required composition

IDs are case-sensitive Machine identities, not GameObject display names.

| Domain | Required typed IDs | Authoring source |
| --- | --- | --- |
| Object3D instance | `object:ball01` | One Machine instance referencing your actual ball Object3D asset |
| Start anchor | `anchor:rackBall01` | Machine anchor; position and XYZ Euler degrees in Machine space |
| Collection anchor | `anchor:traySlot01` | Machine anchor on a physically supported collection area |
| Pocket triggers | `trigger:Pocket_XNeg_Middle`, `trigger:Pocket_XNeg_YNeg`, `trigger:Pocket_XNeg_YPos`, `trigger:Pocket_XPos_Middle`, `trigger:Pocket_XPos_YNeg`, `trigger:Pocket_XPos_YPos` | Existing Cabinet GLB semantic nodes/meshes `OasisTrigger_<id>`; winning node name takes precedence over mesh name |
| Logical inputs | `input:rerack`, `input:newGame` | Machine input declarations |

The canonical pocket IDs match the six Blender object names supplied in the screenshot.
The exported GLB has not been inspected; confirm it actually contains all six meshes.
`OasisTrigger_` is removed when deriving the logical ID, so
`OasisTrigger_Pocket_XNeg_Middle` becomes `trigger:Pocket_XNeg_Middle`. Preserve
spelling and case. A semantic node name takes precedence over a semantic mesh name;
the mesh name is used only if the node name is not semantic. All six pockets perform
the same collection action, so no left/right/far coordinate mapping is required.
No other ball, cue-ball, rack or tray IDs are required.

Use the existing Project/Library reference picker for the real Cabinet and ball
asset manifests. Authored assets belong under `Assets/`; keep intrinsic model
scale/up-axis in the Object3D definition. The ball needs a working collider and an
enabled dynamic Rigidbody on its authoritative live root. Position rackBall01 and
traySlot01 at distinct Machine-space locations, above suitable supporting collision
geometry and outside all pocket volumes.
Collection zeros motion once but leaves the ball active and dynamic: provide
physical support/containment rather than assuming the script freezes it.

## Confirm trigger names before building

Export all six actual Blender trigger meshes into the referenced Cabinet GLB, then
reload the Cabinet in Editor. Inspect discovered IDs where the named trigger inventory
is available, and confirm the six `trigger:` references in the composition table above.
If a mesh was renamed or excluded during export, re-export and reload/refresh discovery
before validating the Machine-owned `behavior.oasis`. Clear Machine reference diagnostics,
save/build and reload Player; an existing generated build does not update automatically.
Do not edit generated output. Asset paths and actual GLB contents remain local verification.

## Behaviour

Startup, rerack and newGame clear the flag, activate ball01, place it at rackBall01
and clear linear and angular velocities. NewGame shares rerack's outcome for this
proving case. Each reset handler is explicit; small duplication keeps the V1 script
readable without helper infrastructure.

One bound trigger/object handler accepts only the six pockets and ball01. The first
entry teleports to traySlot01, clears both velocities and sets collected to true.
The ball remains active and visible. Later callbacks from any pocket do nothing,
including no further teleport or velocity clearing. Unrelated triggers/objects and
trigger exits do nothing. Reset allows collection again; reload creates fresh state.

Full multi-ball collection and cue-ball handling remain follow-up work. Scoring,
full Pool rules, aiming, cue animation, shot controls and multiplayer are deferred.
The compiler, interpreter, A7 API, runtime adapter and schemas are unchanged.

## Connect and verify manually

1. Open your actual Machine in Editor, select Runtime **Oasis**, add Oasis Script
   Behaviour and replace its source with this file. The authored package stores
   `behavior.oasis` beside its Machine manifest. Assign the actual Cabinet and one
   ball asset; declare the exact instance and anchors above. In **Window > Input Map**,
   choose **Add Input**, enter logical ID `rerack` and an optional display name, then
   **Create**. Repeat for `newGame`. Use raw IDs without `input:`; the read-only
   **Logical ID** column is authoritative for script references, not Name. No MFME
   button number, coin channel, linked visual or imported metadata is required.
   IDs are case-sensitive; blank/invalid/duplicate IDs show feedback without edits.
   Cancel leaves the Machine unchanged; additions participate in Machine undo/redo.
   Match all six
   Cabinet trigger semantics. Keep unrelated authored data intact.
2. Save work in progress at any point, including while syntax/type errors or unresolved
   references remain. Save/Save As preserve the manifest and `behavior.oasis`; errors
   remain visible after saving and reopening. A successful save clears dirty state.
   A declared but missing source still requires explicit replacement before saving.
   Once core and Machine-aware script diagnostics both pass, save, then build.
   Build revalidates persisted source and exports `behavior/behavior.oasis` under
   Machine runtime schema 9. Do not edit/commit disposable generated Player builds.
3. **Startup:** load in Player; check ball01 is active at rackBall01 with both
   Rigidbody velocities zero and no script fault.
4. **Pocket collection:** roll/drop it into a declared pocket; check it reaches
   traySlot01, stays visible/active and has both velocities cleared. Repeated or
   overlapping callbacks must not move it again.
5. **Reset:** press/release R (rerack), then repeat with N (newGame). Each should
   restore the ball to rackBall01 and clear both velocities.
6. **Recollection:** after each reset, enter a pocket again and confirm collection.
   Exercise all six pockets, resetting between them. Reload and repeat; also load
   an Emulation Machine to confirm normal loading without a script session.

## Development keyboard controls

Editor/development preview adds `RuntimeInputDevelopmentControls` to its loaded
session root beside the existing development controls. In the Unity Inspector,
expand **Bindings** and add `R → rerack` and `N → newGame`; focus the Game view.
These runtime Inspector settings must be reapplied after reload. Bindings default
empty, incomplete/undeclared IDs are ignored, and the bridge is excluded from release
builds. Input Map now authors logical declarations; its Key column does not supply
production Player bindings. The broader named-input/binding redesign remains deferred,
and there is no new serialized Machine format.

The retained generic bridge calls `RuntimeMachine.SetInputState` on held-key
transitions, aggregates multiple keys for one input, releases on disable/focus loss/
reconfiguration, and detaches from an unloaded Machine. Use it as the sole
development keyboard owner of its bound logical inputs.

## Editor authoring regression checklist

On Windows/.NET 9, also verify these authoring fixes before the gameplay checklist:

1. Paste this script into an incomplete Oasis Machine, Save and Save As, then reopen
   each package. Confirm exact source preservation, clean dirty state/path updates,
   and unchanged diagnostic codes/locations. Repeat with a syntax error and a type
   error, such as `object.reset(1);`. Storage/package failures must still report errors.
2. In an empty Input Map, add `rerack` and `newGame` with explicit IDs; confirm their
   OSM3004 errors clear. Undo each addition and confirm its error returns, then redo
   and confirm it clears. Save/reopen and inspect the IDs and display names.
3. Try blank, `input:rerack`, whitespace/invalid and duplicate IDs, then Cancel.
   Confirm no dirty change or undo entry. Switch Machines during creation, and close
   the active Machine: no pending form may add to either the old or wrong Machine;
   Add Input is disabled with no Machine context. Confirm imported rows still edit
   and delete normally. Check System, Light and Dark themes.
4. Complete the actual composition, save and build successfully. Inspect exported
   input IDs. Save an invalid script, then build again: rejection must preserve every
   file in the previous runtime package. Restore valid source and rebuild. Separately
   remove a declared sidecar: Save/Save As must not silently create an empty source.
5. Load the valid build, configure **R → rerack** and **N → newGame** on its development
   session root, and follow startup → pocket → reset → recollection above. Reapply
   bindings after reload and confirm normal Emulation compatibility.

## Automated coverage and validation status

PoolBehaviorTests, PoolBehaviorReferenceTests and PoolBehaviorIntegrationTests
continue reading the canonical committed file (linked test output for .NET; direct
repository path for Unity). They cover startup, each pocket, same/different-pocket
duplicates, unrelated payloads, both reset inputs, recollection and fresh reload
state. Live A7 tests use registered roots/Rigidbodies and existing trigger relays;
development keyboard transition/lifecycle coverage is retained.

Focused Editor coverage is in `MachineBehaviorAuthoringTests`,
`InputCreationViewModelTests` and `MachineRuntimeBuildServiceTests`: permissive
Save/Save As and reopen, unchanged diagnostics/dirty state, missing-source protection,
strict rejection preserving every runtime package file, explicit input IDs and export,
validation/cancellation/context targeting, and diagnostic updates through undo/redo.

Run the full Oasis.Scripting.Tests and OasisEditor.Tests locally on Windows/.NET 9,
and Player EditMode tests in Unity 6000.0.47f1. These suites and the actual asset
manual checklist have not been executed in Codex: AGENTS.md prohibits builds and
test execution here. Static checks do not establish a passing .NET or Unity suite.
A8.6 fruit-device commands and A8.7 Whac-A-Mole validation remain separate work.
