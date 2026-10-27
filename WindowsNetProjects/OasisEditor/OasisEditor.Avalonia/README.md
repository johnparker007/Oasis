# Avalonia Editor migration

The parallel Editor now supports native project creation, Panel2D authoring/import,
Face generation/artwork processing, specialist 2D rendering, full non-3D Machine
settings, Player build/export/launch, real native preview, Cabinet/Prefab/Machine
3D authoring views and workspace persistence.
WPF remains available for its other asset editors. Authored schemas and Player files
are unchanged by this migration.

## Delivered

- Unity-inspired docking workspace with Hierarchy, Inspector, searchable Project
  pane and authoring viewport; semantic System/Dark/Light themes.
- Native schema-9 project scaffolding shared with WPF, including the real schema-11
  default Machine and standard folders. Existing destinations are refused, with
  atomic reservation for concurrent creation.
- Asset discovery across Panels, Faces, Machines, Cabinets, Reels and Object3D.
  Double-click Panels/Faces/Machines/Cabinets/Object3D Prefabs to author them.
  Reel creation/editing is also available in Avalonia, with a physical-dimension
  GPU preview, stable native IDs, history, atomic saves and workspace restoration.
  Reel symbol-band artwork remains a Face property; the native Reel asset stores
  its physical diameter and width. Library browsing/selection remains the next slice.
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
- Native Cabinet3D and schema-2 Object3D Prefab sessions are available in Avalonia.
  The Cabinet inspector edits model scale/up-axis and target settings; the Prefab
  inspector edits typed node transforms and hierarchy plus Model, Collider and
  Rigidbody components. Edits retain native IDs/metadata, use document-scoped
  undo/redo, validate package contents and protect external edits. These packages
  retain the existing schemas and can be consumed by WPF and runtime builds.
- Machine, Cabinet and Prefab documents compose into the same interactive 3D scene.
  The scene displays GLB meshes/materials, Cabinet semantic geometry, supported box
  and sphere colliders, triggers, Machine anchors and assigned Face surfaces. Mounted
  Faces use their authored artwork and can display actual preview outputs when those
  outputs are available. Capsule and Mesh collider previews currently report a
  diagnostic without drawing an approximation. GLB skinning and animation are not
  evaluated; the loader reports diagnostics for both.
- Authored Cabinet planar reflections project up to four mounted Face sources onto
  the selected receiver material, with separate base/lamp strength, orientation,
  Fresnel, roughness, distortion, edge fade and optional visibility masks. Automatic
  planes follow the current imported Face target. Mounted Face buttons resolve
  authored Machine inputs through the triangle hit UVs during native preview.
- The 3D view renders offscreen through a real GPU context and is shared by desktop
  display and background capture. Source paths select Avalonia ANGLE/EGL on Windows,
  surfaceless Mesa EGL with default EGL fallback on Linux, and CGL on macOS. Workspace
  settings retain the active 3D mode and Cabinet/Prefab manifest path, hierarchy
  selection, camera and visual/collider/trigger visibility while remaining compatible
  with earlier version-1 settings that have no 3D section.
- Headless MCP includes `inspect_scene3d`, Cabinet and Prefab open/create/edit/save
  tools, hierarchy selection/picking, camera and visibility controls, scene reload,
  scene history and explicit scene-library-root selection. It returns the actual
  scene hierarchy, bounds, diagnostics, selection, camera, visibility and native
  serialized document; capture uses the same GPU scene view.
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
- Production Player build and portable folder export share `Oasis.RuntimeBuild`
  with WPF: Machine/script/Cabinet validation, Face textures, prefabs, reel bands
  and the current runtime contract. Export refuses existing destinations and unsafe
  paths; UI progress is modal and cancellable. MCP serializes builds/exports, confines
  output to Generated and checks the active saved source before and after work.
  Player launch builds first, uses an explicitly chosen executable, and accepts
  explicit Fabric/Amber libraries for the Player's supported Impact backend.

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
File > Build Player machine and Export portable Player package use the open saved
Machine. Launch in Oasis Player supports Oasis behavior and JPM Impact; other native
platforms currently have integrated Editor preview but no Unity Player backend.
Set Oasis Library folder locates library-scoped asset dependencies for desktop builds.

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
`build_machine_for_player` and `export_machine_player_package` use the production
pipeline and return output/error details without launching a Player. Library-scoped
dependencies require an explicit `libraryRoot`; headless builds do not load personal
library preferences.

```powershell
python WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_mcp.py --output <capture-directory>
```

The standard-library client creates a native disposable project, authors Panels,
images, source regions and Faces, checks history/save/reopen, and captures multiple
themes/sizes. Optional `--fml <layout.fml>` imports a disposable real-layout copy.
Optional `--project <existing.oasisproj> --panel <asset.panel2d> --face <asset.face>`
adds read-only existing-content captures. Never commit captures/private assets/ROMs.
With `--project`, `--cabinet <relative manifest>`, `--prefab <relative manifest>`
and `--scene-machine <relative manifest>` capture the real GPU views in both themes
and with semantic overlays. `--library-root <directory>` resolves Library references.

## Verification and remaining migration

Previously verified on Windows before 3D integration: MCP build and headless
verification; all 96 Avalonia tests,
including source-shape Face generation, artwork import/registration/calibration,
override processing, pixel-restoring undo, renderer pixel checks and workspace
roundtrips; all 1,449 WPF regression tests; actual MCP project/Face/Machine
history/save/reopen and rendered captures. The generated artwork capture was
visually checked. The integrated preview also passed actual native Impact execution:
512 lamp channels, eight reels, 32 display cells, nonzero PCM, authored service-door
delivery, pause/resume/reset/stop/restart and inspected headless Panel/Face captures.
The reference game reports its native error display; credited play is not verified.
Production build/export also passed on a disposable copy of the actual authored
Impact project, including mounted Faces, Cabinet, reel and real ROM dependencies.
The automated checks did not launch a visible Player process.
After 3D integration, 117/117 Avalonia tests, 9/9 portable GLB/composition tests and
1,449/1,449 WPF tests pass on Windows. This includes actual ANGLE GPU depth, texture
orientation/cutout, transparency, mirrored rendering/picking, reflection projection
and headless-control capture checks. Workspace roundtrips cover Cabinet/Prefab paths,
selection, camera/visibility, invalid-path preflight and legacy version-1 settings.
Real Pool Cabinet/Prefab/Machine and mounted Impact Face scenes were captured through
MCP and inspected. Real Impact preview changed both mounted Face output fingerprints,
with 512 lamp channels, eight reels, 32 display cells and nonzero silently consumed
PCM. No credited play or audible playback is claimed. Linux/macOS execution and
physical desktop interaction remain unverified.

Separate future migration work includes multi-document tabs and Reel/library UI.
The scene is a static authoring preview: GLB skinning/animation and Capsule/Mesh
collider visualization remain unsupported as described above.

Framework-dependent Linux x64 and Apple Silicon macOS publication succeeds, including
matching Skia libraries. Execution and distribution packaging on those systems remain
unverified, as do native docking/floating, OS dialogs,
clipboard, physical input/focus and mixed-DPI monitors; specialist visual parity,
audible playback and credited gameplay. Headless images verify that backend's layout,
not these desktop/platform behaviours.
