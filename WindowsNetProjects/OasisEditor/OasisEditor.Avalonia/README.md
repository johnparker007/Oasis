# Avalonia Editor migration

The parallel Editor now supports native project creation, Panel2D authoring/import
and Face editing. WPF remains the complete application while further workflows are
extracted. No authored schemas or Player files change in this migration.

## Delivered

- Unity-inspired docking workspace with Hierarchy, Inspector, searchable Project
  pane and authoring viewport; semantic System/Dark/Light themes.
- Native schema-9 project scaffolding shared with WPF, including the real schema-11
  default Machine and standard folders. Existing destinations are refused, with
  atomic reservation for concurrent creation.
- Asset discovery across Panels, Faces, Machines, Cabinets, Reels and Object3D.
  Double-click Panels/Faces to author them. Other asset editors remain in WPF.
- Panel rectangle/anchor/zone/label creation and image import; selection and multi-
  selection; atomic group movement; duplication with fresh IDs; deletion; stacking
  order; drag movement/resize and inspector geometry/visibility/lock editing.
- Persisted Face source-region creation, convex four-corner editing and deletion,
  with region outlines in the viewport. Generate Face creates a native package from
  a saved source shape using perspective correction, lamp masks, semantic components
  and tray authoring.
- Actual FML decoder, mapper, asset copier, lamp/mask and background compositing
  shared with WPF. Portable Skia bitmap adapters preserve palette alpha and straight
  color bytes. Imports have fresh IDs and enter history as one command; existing
  import packages are refused. Failure cleanup only removes owned reserved output.
- Recovered FML input definitions are exposed as session metadata for Machine setup.
  Panel2D schema 3 does not persist Machine input bindings.
- Blank native Face creation; full current-schema Face loading; lamp-window creation;
  element/emitter name, geometry, visibility, locking and lamp-ID editing; deletion,
  history and save. The viewport shows actual artwork and editable bounds, without
  simulating specialist lamp/reel/display rendering or emulation.
- Face artwork import and override import; saved perspective registration, sampled or
  manual calibration, spatial/tonal processing and override alignment; rebuild
  Correction Input, Base and Output; recipe undo/redo restores generated pixels.
  These operations are shared by the Avalonia UI and workspace-confined MCP tools.
- Full project/Machine/Panel2D/Face models and storage in `Oasis.Authoring.Core`.
  Face generation, perspective mapping/rasterization, semantic conversion,
  processing/calibration, override, tray/emitter authoring and image import are
  shared services. WPF uses narrow progress/reference/geometry compatibility
  adapters; Core/decoder/Avalonia have no WPF dependency.
- Exact saved-state tracking and document-local history. Panel save detects external
  manifest changes; Face save fingerprints the authored package. Dirty replacement
  and close guards include both retained documents. One Panel and one Face may be
  retained, with one active view. Imported images remain after undo for redo.

New sessions accept current schemas and reject unrecognized fields rather than
silently discarding data. Full DTO metadata is retained through editing.

## Build and use

From the repository root with .NET 9:

```powershell
dotnet build WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp
dotnet test WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Tests
```

Only launch this when a visible application is wanted:

```powershell
dotnet run --project WindowsNetProjects/OasisEditor/OasisEditor.Avalonia -- --workspace <projects-directory> --project <project.oasisproj>
```

New projects are created under the explicit workspace. File creates/opens projects
and imports an FML into the open panel. Project creates Panels and Faces; Create
adds elements/images/regions/lamps. Create Face starts blank; Generate Face builds a
Face from the selected saved source region. File includes artwork and override import,
registration/processing and rebuild commands.

Wheel zooms, middle drag pans, drag moves selection and the bottom-right handle
resizes a single element. Ctrl/Cmd-click toggles selection. Ctrl/Cmd+S saves;
Ctrl/Cmd+Z and Ctrl/Cmd+Shift+Z undo/redo; Ctrl/Cmd+D duplicates, Ctrl/Cmd+A selects
all panel elements, and Delete removes selection outside text fields. Source regions
expose four corners in Inspector. Save or undo pending edits before replacement.

Native-picker imports can read the explicitly selected external file; the UI stages
it under Generated before calling the confined session API. MCP imports require
sources inside the explicit workspace. User-owned source files are never modified.

## Background MCP verification

```text
dotnet <absolute-path>/OasisEditor.Avalonia.Mcp.dll --workspace <existing-directory>
```

Project/Panel tools cover project creation/open, asset inspection, panel creation/open,
element/image/FML import, multi-selection, move/duplicate/reorder/delete, source-region
editing, undo/redo and save. Face tools cover create/open, select, add lamp, edit,
delete, history, generation, artwork/override import, recipe updates, rebuild and
inspection. `capture_workspace` returns a real Headless + Skia PNG of
the same controls. It creates no OS windows and never captures the desktop or sends
system input. Sizes are 960–1920 by 640–1200; themes System/Light/Dark. The existing
WPF MCP connection and user settings remain unchanged.

```powershell
python WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_mcp.py --output <capture-directory>
```

The standard-library client creates a native disposable project, authors Panels,
images, source regions and Faces, checks history/save/reopen, and captures multiple
themes/sizes. Optional `--fml <layout.fml>` imports a disposable real-layout copy.
Optional `--project <existing.oasisproj> --panel <asset.panel2d> --face <asset.face>`
adds read-only existing-content captures. Never commit captures/private assets/ROMs.

## Verification and remaining migration

Verified on Windows: portable authoring/headless UI tests; all 1,448 existing WPF
tests, including 110 focused import/mapper/pixel/shortcut tests; actual MCP authoring
and rendered captures. A disposable Bonanza FML produced 195 editable elements.
An existing 196-element Panel and 80-element Face were captured read-only. Existing
decoder nullable/obsolete warnings now appear in Core on a clean build. No native
emulation was executed for this migration.

Next: specialist 2D renderers, remaining inspectors and Machine settings, build/export,
real Fabric/Amber preview, multi-document tabs, saved layouts and Reel/library UI.
3D remains explicitly deferred until its renderer is selected and proved portable.

Not yet verified: Mac/Linux execution/packaging; native docking/floating, OS dialogs,
clipboard, physical input/focus and mixed-DPI monitors; specialist visual parity,
audio or native emulation in Avalonia. Headless images verify that backend's layout,
not these desktop/platform behaviours.
