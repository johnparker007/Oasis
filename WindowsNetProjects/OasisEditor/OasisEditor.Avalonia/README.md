# Avalonia Editor migration

The parallel Editor now supports native project creation, Panel2D authoring/import,
Face editing and full non-3D Machine settings. WPF remains available for remaining
workflows. No authored schemas or Player files change in this migration.

## Delivered

- Unity-inspired docking workspace with Hierarchy, Inspector, searchable Project
  pane and authoring viewport; semantic System/Dark/Light themes.
- Native schema-9 project scaffolding shared with WPF, including the real schema-11
  default Machine and standard folders. Existing destinations are refused, with
  atomic reservation for concurrent creation.
- Asset discovery across Panels, Faces, Machines, Cabinets, Reels and Object3D.
  Double-click Panels/Faces/Machines to author them. Other asset editors remain in WPF.
- Panel rectangle/anchor/zone/label creation and image import; selection and multi-
  selection; atomic group movement; duplication with fresh IDs; deletion; stacking
  order; drag movement/resize and inspector geometry/visibility/lock editing.
- Persisted Face source-region creation, convex four-corner editing and deletion,
  with region outlines in the viewport. Face generation from these regions is pending.
- Actual FML decoder, mapper, asset copier, lamp/mask and background compositing
  shared with WPF. Portable Skia bitmap adapters preserve palette alpha and straight
  color bytes. Imports have fresh IDs and enter history as one command; existing
  import packages are refused. Failure cleanup only removes owned reserved output.
- Full current-schema Machine loading/creation, typed settings for every supported
  Emulation platform, Oasis runtime/script editing with real compiler diagnostics,
  interaction and input authoring, and cabinet/Face/reel/prefab/anchor/surface
  assignments. Recovered FML input definitions remain separate until intentionally
  applied to Machine history. ROM and platform values stay explicit; no game mapping
  is guessed and native emulation is not previewed.
- Blank native Face creation; full current-schema Face loading; lamp-window creation;
  element/emitter name, geometry, visibility, locking and lamp-ID editing; deletion,
  history and save. The viewport shows actual artwork and editable bounds, without
  simulating specialist lamp/reel/display rendering or emulation.
- Full project/Machine/Panel2D/Face models and storage in `Oasis.Authoring.Core`.
  The core/decoder/Avalonia projects have no WPF dependency. WPF views, library
  catalog and region conversion adapters remain platform-specific.
- Exact saved-state tracking and document-local history. Panel and Machine saves
  detect external manifest/script changes; Face save fingerprints the authored
  package. Dirty replacement and close guards include retained documents. One Panel,
  Face and Machine may be retained, with one active view. Imported images remain
  after undo for redo.

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

New projects are created under the explicit workspace. File creates/opens projects,
Machines and imports an FML into the open panel. Project creates Panels, Faces and
Machines; Create adds elements/images/regions/lamps. Face creation currently starts
blank.

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
delete, history and save. `capture_workspace` returns a real Headless + Skia PNG of
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

Verified on Windows: Avalonia MCP build; all 54 Avalonia tests, including 14 Machine
roundtrip/history/conflict/script/headless-inspector checks; actual MCP project,
Machine settings/history/save/reopen and rendered captures. Earlier Panels/Face/FML
checks remain covered by the same suite. No native emulation was executed for this
migration.

Next: Face generation from source regions, artwork import/registration/processing,
specialist 2D renderers, build/export, real Fabric/Amber preview, multi-document
tabs, saved layouts and Reel/library UI. Machine 3D rendering remains explicitly
deferred until its renderer is selected and proved portable.

Not yet verified: Mac/Linux execution/packaging; native docking/floating, OS dialogs,
clipboard, physical input/focus and mixed-DPI monitors; specialist visual parity,
audio or native emulation in Avalonia. Headless images verify that backend's layout,
not these desktop/platform behaviours.
