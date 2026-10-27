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
  with region outlines in the viewport. Generate Face creates a native package from
  a saved source shape using perspective correction, lamp masks, semantic components
  and tray authoring.
- Actual FML decoder, mapper, asset copier, lamp/mask and background compositing
  shared with WPF. Portable Skia bitmap adapters preserve palette alpha and straight
  color bytes. Imports have fresh IDs and enter history as one command; existing
  import packages are refused. Failure cleanup only removes owned reserved output.
- Full current-schema Machine loading/creation, typed settings for every supported
  Emulation platform, Oasis runtime/script editing with real compiler diagnostics,
  interaction and input authoring, and cabinet/Face/reel/prefab/anchor/surface
  assignments. Recovered FML input definitions remain separate until intentionally
  applied to Machine history. ROM and platform values stay explicit; no game mapping
  is guessed.
- Blank native Face creation; full current-schema Face loading; lamp-window creation;
  element/emitter name, geometry, visibility, locking and lamp-ID editing; deletion,
  history and save. The viewport shows actual artwork, lamps, buttons, displays, reel
  mounts and editable bounds.
  The specialist views reuse the production Skia lamp, reel, segment/alpha/VFD and
  Face compositor renderers, with bundled fonts/display definitions and cache
  invalidation for changed artwork and live output snapshots.
- Face artwork import and override import; saved perspective registration, sampled or
  manual calibration, spatial/tonal processing and override alignment; rebuild
  Correction Input, Base and Output; recipe undo/redo restores generated pixels.
  These operations are shared by the Avalonia UI and workspace-confined MCP tools.
- Full project/Machine/Panel2D/Face models and storage in `Oasis.Authoring.Core`.
  Face generation, perspective mapping/rasterization, semantic conversion,
  processing/calibration, override, tray/emitter authoring and image import are
  shared services. WPF uses narrow progress/reference/geometry compatibility
  adapters; Core/decoder/Avalonia have no WPF dependency.
- Machine settings, source and interaction authoring are shared with WPF; WPF views,
  library catalog and region conversion adapters remain platform-specific.
- Exact saved-state tracking and document-local history. Panel and Machine saves
  detect external manifest/script changes; Face save fingerprints the authored
  package. Dirty replacement and close guards include retained documents. One Panel,
  Face and Machine may be retained, with one active view. Imported images remain
  after undo for redo.
- Workspace persistence saves docking topology (including pinned/floating panes),
  proportions, theme, window placement, asset search, project/document paths and
  selection. Restore validates layout and content before replacing clean documents;
  unsaved edits and external settings changes are protected. Reset Layout keeps
  document contents. Offscreen windows are moved back to a connected screen.
- Real Fabric/Amber preview uses the production backend and current Machine settings.
  Play/pause/resume/reset/stop/throttle controls feed actual native lamp, reel and
  display outputs into Panel2D/Face views. Authored keyboard and visual input links
  use logical press/release and the separate native coin protocol. Focus loss,
  pause, reset and shutdown release held inputs; key repeat does not repeat coins.
  Source changes stop preview, and startup rejects unsaved or externally changed
  Machine sources. One native preview is allowed per process.
- Native library paths and the retained Machine are included in workspace settings.
  Restoring a workspace never resumes emulation. Windows desktop playback can be
  explicitly enabled; headless MCP always consumes actual PCM silently. Other
  platforms need matching native providers; audible playback currently uses WASAPI.

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
Machines and imports an FML into the open panel. Project creates Panels, Faces and
Machines; Create
adds elements/images/regions/lamps. Create Face starts blank; Generate Face builds a
Face from the selected saved source region. File includes artwork and override import,
registration/processing and rebuild commands.
File also creates/opens Machines; Project creates Panels, Faces and Machines.

File > Emulation preview configures Fabric and platform provider libraries. Open a
saved configured Machine, then use Play/Pause/Stop or the preview menu. Select a
Panel/Face to view live outputs. Machine inputs opens the authored input controls;
configured shortcuts and linked Panel/Face buttons also route inputs. New Panel
element IDs use the native GUID format required by existing visual input links.

Wheel zooms, middle drag pans, drag moves selection and the bottom-right handle
resizes a single element. Ctrl/Cmd-click toggles selection. Ctrl/Cmd+S saves;
Ctrl/Cmd+Z and Ctrl/Cmd+Shift+Z undo/redo; Ctrl/Cmd+D duplicates, Ctrl/Cmd+A selects
all panel elements, and Delete removes selection outside text fields. Source regions
expose four corners in Inspector. Save or undo pending edits before replacement.

Window provides Save Workspace, Restore Workspace and Reset Layout. The desktop
application restores settings from the local application-data folder
`Oasis/AvaloniaEditor/workspace.json` at startup and saves on a clean close. Invalid
or externally changed settings are preserved. A deliberately minimized window is
reopened normally. Headless MCP sessions never read/write those personal settings;
their `save_workspace`/`restore_workspace` tools require an explicit workspace path.

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
inspection. Machine tools author settings, inputs, behavior and save. Layout tools
inspect/save/restore/reset the real model. `capture_workspace` returns a real
Headless + Skia PNG of the same controls. It creates no OS windows and never captures the desktop or sends
system input, with additional images for floating panes. Sizes are 960–1920 by
640–1200; themes System/Light/Dark. The existing
WPF MCP connection and user settings remain unchanged.

`start_preview`, `inspect_preview`, `control_preview` and `preview_input` control
only the native backend owned by that headless session. The optional
`verify_native_preview.py --help` client verifies a saved Emulation Machine with
explicit native libraries and optional Panel/Face captures, without authored writes.
It tests lifecycle and an optional exact non-coin input ID; it never invents game
mappings or claims credit/spin/payout from boot or audio/output counters.

```powershell
python WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_mcp.py --output <capture-directory>
```

The standard-library client creates a native disposable project, authors Panels,
images, source regions and Faces, checks history/save/reopen, and captures multiple
themes/sizes. Optional `--fml <layout.fml>` imports a disposable real-layout copy.
Optional `--project <existing.oasisproj> --panel <asset.panel2d> --face <asset.face>`
adds read-only existing-content captures. Never commit captures/private assets/ROMs.

## Verification and remaining migration

Verified on Windows: MCP build and headless verification; all 73 Avalonia tests,
including source-shape Face generation, artwork import/registration/calibration,
override processing, pixel-restoring undo, renderer pixel checks and workspace
roundtrips; all 1,448 WPF regression tests; actual MCP project/Face/Machine
history/save/reopen and rendered captures. The generated artwork capture was
visually checked. The integrated preview also passed actual native Impact execution:
512 lamp channels, eight reels, 32 display cells, nonzero PCM, authored service-door
delivery, pause/resume/reset/stop/restart and inspected headless Panel/Face captures.
The reference game reports its native error display; credited play is not verified.

Next: build/export, multi-document tabs, and Reel/library UI.
Machine 3D rendering remains explicitly deferred until its renderer is selected and
proved portable.

Framework-dependent Linux x64 and Apple Silicon macOS publication succeeds, including
matching Skia libraries. Execution and distribution packaging on those systems remain
unverified, as do native docking/floating, OS dialogs,
clipboard, physical input/focus and mixed-DPI monitors; specialist visual parity,
audible playback and credited gameplay. Headless images verify that backend's layout,
not these desktop/platform behaviours.
