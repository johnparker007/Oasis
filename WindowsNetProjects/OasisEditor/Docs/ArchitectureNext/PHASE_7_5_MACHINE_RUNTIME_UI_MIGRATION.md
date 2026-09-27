# Architecture Next Phase 7.5 — Machine Runtime Settings UI Migration

## Result

Machine-owned emulation configuration now has one authoring surface: the owning Machine document's **Details > Runtime > Runtime Settings** expander. `Edit > Project Settings` remains as a truthful, informational view of project name, project file, and Assets folder. Phase 8 Installation work was not started.

## Repository-grounded pre-implementation audit

Phase 7 had already made `MachineDocument.Runtime` authoritative. Its `EmulationRuntimeDefinition` owns `Platform` and the concrete `PlatformSettings`. Nevertheless, `ProjectSettingsView` still displayed a duplicate platform selector and every runtime editor. The view inherited `MainWindowViewModel`, whose `ActiveSettings<T>()`, `UpdateActiveRuntime(...)`, save callbacks, ROM browse commands, reset command, and selected native tab routed edits to the currently active Machine and marked that document dirty. Thus persistence was Machine-scoped but presentation and transient editing state were shell/project-scoped. Switching active documents rebuilt shell projections, which obscured rather than expressed multi-Machine ownership.

The dedicated MPU5, Epoch, MPU3, M1, and Scorpion 4 views contained useful platform layouts but depended on property names supplied by the shell DataContext. Impact/System 6 was embedded directly in `ProjectSettingsView`. The Edit menu, `OpenProjectSettings()`, `EditorToolWindowId.ProjectSettings`, and tool-window factory remain useful for genuine project information and were therefore retained.

The current `.oasisproj` schema 9 contains workspace metadata/layout only. Runtime fields survive only in unused historical helper methods, not in the current serialized project shape. Machine schema 4 is already the sole authored runtime persistence format.

## Final Machine Runtime UI

The existing Overview/Details structure is unchanged. Details keeps the compact composition form and shows:

- read-only Runtime Type (`Emulation`);
- the supported Platform selector;
- a collapsed-by-default Runtime Settings expander with bounded content;
- a compact no-configuration message for `None`;
- the matching editor for Impact, MPU5, Epoch, MPU3, Maygay M1, or Scorpion 4.

The expander prevents ROM/hardware configuration from pushing Cabinet, Face, physical Reel assignment, and Inputs through several screen heights. Physical Machine Reel assignments remain composition references to reusable Reel assets. Emulator steps/opto/hardware configuration remains inside emulation settings.

## View-model ownership

Each Machine `DocumentTabViewModel` now creates exactly one `MachineRuntimeSettingsViewModel`. It reads that tab's current `MachineDocument.EmulationRuntime`, creates editor projections from cloned settings, and commits through the owning tab's `ExecuteMachineMutation` command seam. It never selects a Machine through `MainWindowViewModel`.

A Machine mutation refreshes that same document's runtime editor. Consequently platform switch, undo, and redo discard stale platform projections and immediately construct the projection matching the restored authoritative settings. ROM browse commands resolve paths using the owning document's project context and commit only to that document.

Shell-level `SelectedFruitMachinePlatform` and active-runtime helpers remain for Play View, Fabric/Amber launch, runtime output/input routing, and active emulation session behavior. They are no longer the DataContext for the Machine authoring surface. The Machine document is the only authority; editor view models are disposable projections, not a second persisted copy.

## Impact/System 6 extraction and reused views

The Impact editor was extracted into `ImpactRuntimeSettingsView`. It retains ROMS, Stake/Prize, Reels, and Coins navigation, ROM browse, flash/percentage, reel-opto reset, coin/mech and hardware fields. UI state was renamed from native *Project Settings* tabs to runtime-settings terminology.

`Mpu5FabricSettingsView`, `EpochFabricSettingsView`, `Mpu3FabricSettingsView`, `M1FabricSettingsView`, and `Scorpion4FabricSettingsView` are reused unchanged in purpose. Their inherited DataContext is now an explicit document-owned `MachineRuntimeSettingsViewModel`, not the shell.

## Project Settings cleanup

`ProjectSettingsView` no longer contains categories, a Fruit machine Platform selector, runtime tabs, ROM controls, reel/opto controls, coin/hopper controls, or platform-specific Fabric views. It shows only project name, project file, and Assets folder, plus guidance that per-Machine behavior is edited in a Machine document. The command/tool window remains because this project information is still useful; no invented project settings were added.

## Multi-Machine isolation, undo, dirty, and save

Every open Machine tab owns a separate runtime editor bound to its own in-memory Machine. Editing Machine A cannot resolve or mutate Machine B, even when both use the same platform. Representative runtime changes execute a Machine document command, dirty only the owner, and participate in document undo/redo. Text paths commit on the views' established LostFocus behavior; checkbox/selection edits retain their existing property-change granularity. Platform switching remains one command that replaces settings with defaults; undo restores the former platform and complete settings object, and redo restores the new platform/defaults.

Saving continues through normal Machine document save semantics and clears that Machine tab's dirty state. Close/reopen reads schema-4 runtime settings. Project, Cabinet, Face, and physical Reel documents are not runtime persistence targets.

## Schema and runtime impact

No serialized shape changed. Machine remains schema 4, Project remains schema 9, and the generated runtime/Player contract remains schema 6. There is no duplicate Project persistence and no Oasis Player change. Preview/build still traverse `Machine -> EmulationRuntimeDefinition -> EmulationLaunchRequest -> Fabric/Amber` for the intended active Machine.

## Automated coverage

Focused tests assert that Project Settings contains project information and no runtime UI; Machine Details hosts Runtime Type, Platform, the bounded runtime editor, all supported platform views, and the `None` state; Impact retains its four logical sections and runtime terminology; supported platforms remain exact; per-document runtime projections switch correctly; two open Impact Machines retain different ROMs; editing/undo/redo affects only the owner; and the ROM mutation seam targets the owning Machine. Existing Phase 7 runtime serialization, platform-switch undo/redo, preview, build, and supported-platform tests remain applicable.

Per `AGENTS.md`, the container lacks the Windows/.NET/WPF toolchain, so builds and tests must be run locally.

## Manual verification checklist

### Project Settings

1. Open **Edit > Project Settings**.
2. Confirm Fruit machine Platform is gone.
3. Confirm Platform Settings is gone.
4. Confirm ROM/Reel/Coin/Stake controls are gone.
5. Confirm project name, project file, and Assets folder remain.

### Machine A

6. Open Machine A and select Details.
7. Confirm Runtime Type is Emulation.
8. Change Platform using the supported selector.
9. Expand Runtime Settings.
10. Confirm the matching platform editor appears.
11. Edit a ROM path and leave the field.
12. Confirm only Machine A becomes dirty.
13. Save Machine A.
14. Close/reopen it.
15. Confirm the ROM path persists.

### Platform settings

16. Verify Impact ROMS, Stake/Prize, Reels, and Coins.
17. Verify MPU5 appears and saves edits.
18. Verify Epoch appears and saves edits.
19. Verify MPU3 appears and saves edits.
20. Verify Maygay M1 appears and saves edits.
21. Verify Scorpion 4 appears and saves edits.
22. Verify None shows the no-platform state.

### Platform switch

23. Start with a configured Impact Machine.
24. Change to MPU5.
25. Confirm the editor switches immediately.
26. Undo.
27. Confirm Impact and all previous settings return.
28. Redo.
29. Confirm MPU5/default settings return.

### Multiple Machines

30. Open Machine A with one ROM/platform.
31. Open Machine B with a different ROM/platform.
32. Switch tabs repeatedly.
33. Confirm each editor always shows its owner.
34. Edit A.
35. Confirm B is untouched.
36. Save A only.
37. Confirm B's dirty/value state is unchanged.

### Preview and build

38. Open a known working fruit Machine.
39. Configure it only through Machine Details.
40. Start emulation.
41. Confirm Fabric/Amber boots.
42. Confirm lamps, reels, displays, and inputs work.
43. Build an Oasis Player Machine.
44. Confirm build succeeds.
45. Confirm output is unchanged from Phase 7.
46. Launch and verify current fruit-machine behavior.

### Composition regression

47. Confirm Cabinet selection still works.
48. Confirm Face assignments still work.
49. Confirm physical Reel assignments still work.
50. Confirm Open Asset buttons still work.
51. Confirm Overview still works.
52. Confirm the Runtime card updates after Platform changes.

## Deliberately deferred

Phase 8 Installation assets, linked topology, Top Box/Slave semantics, new runtime kinds, video/JAMMA runtime, runtime plugins, generalized Devices, 3D buttons, arbitrary graph editing, and schema changes remain deferred. The linked-Machine example motivates this ownership cleanup but is not implemented here.
