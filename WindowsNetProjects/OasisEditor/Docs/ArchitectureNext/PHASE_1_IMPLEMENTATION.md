# Architecture Next Phase 1 implementation

## Current authored schemas

- Project schema 9 is workspace-only: name, timestamps, `Assets` and `Generated` layout. There is no separate project-root `Machines` path and no platform, ROM, input, Face-mount, or reel-mapping state.
- Machine schema 1 is stored at `Assets/Machines/<Name>/asset.machine`. It owns a stable GUID, display name, Cabinet asset reference, Cabinet-target-to-Face assignments, temporary logical-reel-to-Cabinet-specification assignments, a discriminated Emulation runtime (`platform` plus only that platform's settings), and inputs.
- Cabinet schema 7 owns its model, target configuration, reflection receiver configuration, preview settings, and temporary physical reel specifications/default only. Installed Face and logical reel assignments are not part of Cabinet.
- Cabinet reflection sources serialize `sourceSurfaceTargetId`. A Machine build resolves that target through its surface assignments.
- Machine runtime schema 4 includes Machine identity, Cabinet and Face composition, emulation runtime configuration, and inputs. Oasis Player validates and retains the runtime definition; Player-side Fabric execution is intentionally deferred.

## Phase-1 reel bridge

The authored Face keeps logical `Reel:n` references and placement. Machine maps each logical reel to an embedded specification ID on its selected Cabinet. Machine build resolves this chain and continues to flatten physical width/radius into Face runtime manifests. Cabinet reel specifications are temporary and are scheduled for replacement by first-class Reel assets in Phase 3.

## Manual verification checklist

1. Create a project and confirm `Assets/Machines/<ProjectName>/asset.machine` exists.
2. Open that manifest and confirm a structured Machine editor appears.
3. Set a Cabinet asset reference on Machine.
4. Assign Faces to the Cabinet's `OasisFace_*` target IDs on Machine.
5. Configure logical reel assignments against the selected Cabinet's temporary specifications.
6. Select the platform and configure ROM/runtime settings; confirm the Machine manifest changes.
7. Import MFME into a Panel2D and confirm imported input definitions are written to the active Machine.
8. Open Face and Cabinet independently; confirm neither manifest contains Machine composition assignments.
9. Save, close, and reopen the Project and Machine.
10. Select the saved Machine and use Build/Preview in Oasis Player.
11. Verify Cabinet, assigned Faces, resolved reels, and target-bound reflections render.
12. Create a second Machine with a different platform/ROM configuration, explicitly select each, and confirm settings do not leak.
13. Add an invalid unassigned Face and confirm it does not affect the selected Machine build.
