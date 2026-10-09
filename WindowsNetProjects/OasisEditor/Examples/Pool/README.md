# Single-ball Pool runtime proving case — A8.5

Canonical gameplay: [behavior.oasis](behavior.oasis). This initial proving case uses
one ball, two Machine-space anchors, six existing pocket trigger IDs and two logical
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
| Pocket triggers | `trigger:PocketLeftCorner`, `trigger:PocketLeftMiddle`, `trigger:PocketLeftFarCorner`, `trigger:PocketRightCorner`, `trigger:PocketRightMiddle`, `trigger:PocketRightFarCorner` | Existing Cabinet GLB semantic nodes/meshes `OasisTrigger_<id>`; winning node name takes precedence over mesh name |
| Logical inputs | `input:rerack`, `input:newGame` | Machine input declarations |

The pocket IDs are the retained example contract, not names observed in an available
authored asset. Match them to your actual Cabinet semantics using typed literals.
No other ball, cue-ball, rack or tray IDs are required.

Use the existing Project/Library reference picker for the real Cabinet and ball
asset manifests. Authored assets belong under `Assets/`; keep intrinsic model
scale/up-axis in the Object3D definition. The ball needs a working collider and an
enabled dynamic Rigidbody on its authoritative live root. Position the start and
collection anchors above supporting collision geometry and outside pocket volumes.
Collection zeros motion once but leaves the ball active and dynamic: provide
physical support/containment rather than assuming the script freezes it.

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
   ball asset; declare the exact instance, anchors and inputs above. Match all six
   Cabinet trigger semantics. Keep unrelated authored data intact.
2. Confirm core and Machine-aware script diagnostics both pass, save, then build.
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
builds. There is no new serialized Machine format or input-authoring UI.

The retained generic bridge calls `RuntimeMachine.SetInputState` on held-key
transitions, aggregates multiple keys for one input, releases on disable/focus loss/
reconfiguration, and detaches from an unloaded Machine. Use it as the sole
development keyboard owner of its bound logical inputs.

## Automated coverage and validation status

PoolBehaviorTests, PoolBehaviorReferenceTests and PoolBehaviorIntegrationTests
continue reading the canonical committed file (linked test output for .NET; direct
repository path for Unity). They cover startup, each pocket, same/different-pocket
duplicates, unrelated payloads, both reset inputs, recollection and fresh reload
state. Live A7 tests use registered roots/Rigidbodies and existing trigger relays;
development keyboard transition/lifecycle coverage is retained.

Run the full Oasis.Scripting.Tests and OasisEditor.Tests locally on Windows/.NET 9,
and Player EditMode tests in Unity 6000.0.47f1. These suites and the actual asset
manual checklist have not been executed in Codex: AGENTS.md prohibits builds and
test execution here. Static checks do not establish a passing .NET or Unity suite.
A8.6 fruit-device commands and A8.7 Whac-A-Mole validation remain separate work.
