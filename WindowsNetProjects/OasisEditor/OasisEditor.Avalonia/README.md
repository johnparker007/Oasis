# Avalonia Editor migration

The parallel Editor now supports native project creation, Panel2D authoring/import,
Face generation/artwork processing, specialist 2D rendering, full non-3D Machine
settings, Player build/export/launch, real native preview, Cabinet/Prefab/Machine
3D authoring views and workspace persistence.
WPF remains available alongside the portable editor. Authored schemas and Player files
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
  its physical diameter and width.
- Face Reel mount creation and editing for logical Reel references, symbol-band images,
  stops, scale, offset, reversal, reel lamps, opacity and transmission masks. Image import
  copies into the actual Face package; opaque imports generate a mask, and an explicit
  generation action builds a new mask from the existing band without copying it again.
  Native metadata, history and IDs are preserved. Reel-only undo/redo keeps unchanged
  artwork recipes intact, even when the original derivation source is unavailable.
- Project Assets / Oasis Library browsing and search, explicit Library folder setup,
  background catalog refresh, malformed-package diagnostics and scoped asset choices.
  Shared Cabinet/Reel/Prefab assets open in the same authoring views, with an explicit
  Library indicator; Save updates the shared package. Workspace restoration retains
  the root, browser source and scoped active asset. Authored Machine references remain
  portable root-relative Project/Library references.
- Copy complete reusable Cabinet/Reel/Prefab packages to the Library, or import them
  into a project, preserving native IDs and dependencies. Staged copies reject conflicts,
  source changes, path escapes and links; existing packages are never overwritten.
  Contained GLB resource files are supported for complete packages; single-GLB model
  import still requires embedded resources. Machine Cabinet/Reel/Prefab choices include
  both scopes, preserve missing references and discover logical Reel references from
  assigned Faces. Physical Reel metadata remains distinct from symbol-band images.
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
  package. Dirty replacement and close guards include retained documents. Multiple assets of each type are retained in explicit tabs, with one active view.
  Switching tabs preserves pending edits, selection, history, saved fingerprints and
  viewport state. Save All uses the native guards; Close refuses dirty assets.
  Imported images remain
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
`--reel-library` adds native Reel/Library copy, shared editing, scoped assignment,
restoration, Face symbol-band/mask authoring and actual GPU capture checks (verified
on Windows; the host needs a functioning native graphics backend).
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

The Library slice passed actual MCP copy/edit/import/restoration and Dark/Light GPU
captures of real Cabinet/Prefab and physical Reel assets. Machines using Library
references built/exported through the production services; a copied native Impact
Machine using Library Cabinet/Reel assignments also ran real Fabric/Amber outputs
(512 lamps, eight reels, 32 display cells and nonzero silently consumed PCM).

Retained asset tabs now support Panel2D, Face, Machine, Cabinet, Prefab and Reel
documents in both Project and Library scopes. Each asset tab can float into an independent editor window with its own viewport,
Hierarchy and Inspector, and return to the main authoring dock. The workspace reopens their
saved paths after validating every document. Pending edits are never serialized
into workspace settings. File includes Close Asset, Save All and Close Project,
plus native Face validation and source-Panel navigation.
The final Reel/Library checks also include actual symbol-band import, generated
transmission masks, metadata history and 2D/mounted-3D captures on a copied real Face.
The copied native Impact Machine rebuilt and ran real outputs with those saved assets.
The Reel/Library authoring regression suite passes 155/155 Avalonia checks, with
1,449/1,449 WPF and 9/9 portable 3D checks passing. Builds retain existing shared
Skia deprecation/nullability warnings. No native desktop interaction is claimed.
The additional GPU check keeps one renderer alive while another is disposed.
EGL driver/display ownership lasts for the process, while individual views release
their own contexts and GPU buffers; closing a viewport cannot terminate a display
shared by another renderer or the host UI. CGL context ownership is unchanged.
The scene is a static authoring preview: GLB skinning/animation and Capsule/Mesh
collider visualization remain unsupported as described above.

Framework-dependent Linux x64 and Apple Silicon macOS publication succeeds, including
matching Skia libraries. Execution and distribution packaging on those systems remain
unverified, as do native docking/floating, OS dialogs,
clipboard, physical input/focus and mixed-DPI monitors; specialist visual parity,
audible playback and credited gameplay. Headless images verify that backend's layout,
not these desktop/platform behaviours.

## Additional editing and overview workflows

Panel Create now includes native lamps, reels, seven-segment, alpha and VFD displays.
Their specialist Inspector settings use document history. Edit includes session-local
cut/copy/paste, preserving complete metadata and assigning fresh IDs, plus all four
stacking-order commands. The clipboard is project-scoped. Source-region corners can
be dragged; one valid convex region edit enters history on release.

Face Create includes native buttons, seven-segment and alpha displays with typed
logical/input references. File > Face generation settings exposes the native
mask/tray/sharpening settings. Settings edits invalidate configured generated products;
metadata-only history does not rebuild files. Rebuilding artwork remains explicit.

Machine scenes include a Composition Graph toggle. The read-only graph resolves
actual Cabinet, Face, source Panel, logical Reel and Prefab dependencies, reports
unresolved assets, supports fit/pan/zoom and opens saved assets from their nodes.
It uses current Machine and active Face data; other dependencies are read from disk.
Preferences persist CPU processing mode/worker limits and Player executable/display
settings independently of authored projects. Headless sessions never load personal
preferences; MCP persistence requires an explicit file inside its workspace.

`python OasisEditor.Avalonia.Mcp/verify_remaining_workflows.py --output <directory>`
checks these native workflows and captures the real workspace in Dark and Light.
The existing `verify_mcp.py --reel-library` continues to cover production builds,
package operations, native history and GPU captures. No visible app or desktop input
is used. Physical OS-dialog/focus/clipboard/docking behavior,
macOS/Linux execution and packaging remain separate verification or UI work.

The additional authoring suite passes 174/174 Avalonia tests, including independent
retained histories/cameras/conflicts, missing-file close, workspace preflight,
headless corner dragging and generation build-state invalidation. The existing
1,449 WPF regressions pass. Both MCP clients pass, including the optional
Reel/Library/GPU and production-build regression. Real saved Pool content resolves
nine composition nodes and nine links without diagnostics; Dark/Light captures
were inspected read-only. Existing shared Skia warnings remain. These results
do not establish native OS interaction or execution on macOS/Linux.

## Individual asset windows

Use the ↗ button beside an asset tab, or File > Float active asset. Multiple
Panel2D, Face, Machine, Cabinet, Prefab and Reel assets can remain editable at once.
Shared Library assets retain their explicit bound root and Library indicator.
Each floating editor has its own viewport, Hierarchy and Inspector. Dock asset,
the main tab's Dock button, or closing the floating window returns its document
without saving or discarding pending edits. Explicit Close Asset still refuses
dirty documents. Project change/close and Save All include floating documents.

Ownership moves entirely in memory: native IDs, unsaved content, history/redo,
selection, saved-file/package hashes and recovered Panel FML inputs survive.
Panel windows share a project-scoped internal clipboard. Reopening an asset
activates its existing window. Native preview stops before its editor moves and
never restarts automatically; preview/build guards include all workspace edits.

Workspace settings persist owned windows, their bounds, internal pane layouts,
selection, 2D view/source-region state, 3D camera/visibility and Machine graph mode.
Restore validates all documents and layouts before replacing a clean workspace;
it reopens saved content and never stores pending authored edits. Window bounds
use the same connected-screen clamping as the main workspace. Layout Reset keeps
owned floating editors alive. Child editors do not load or save personal preferences.

The headless MCP host adds float/dock/inspect/history/save and focused floating
Panel/Face/Reel/Cabinet/Prefab/Machine editing tools. `capture_workspace` includes
the actual floating-window PNGs after the main workspace image, without desktop
capture, OS input or visible windows. Reproduce the closed loop with:

```powershell
python OasisEditor.Avalonia.Mcp/verify_floating_documents.py --output <directory>
```

This creates disposable native assets, edits all asset kinds in independent windows,
checks history/save/conflicts, shared Library edits, saved workspace restoration
and redocking, then inspects actual GPU state and Dark/Light captures. It also keeps
one GPU renderer active while sibling windows redock/dispose. Native OS window
dragging, focus, dialogs, mixed-DPI/monitor behavior and macOS/Linux execution remain
unverified; headless captures establish only that backend's rendering and layout.

The floating-window integration passes 184/184 Avalonia checks and 1,449/1,449
WPF regressions on Windows. The dedicated floating-window MCP loop passed with
seven simultaneous editors (all native asset kinds plus a Library Reel), including
real GPU sibling-renderer survival and restored Dark/Light captures. Images were
inspected. The existing Reel/Library and authoring MCP regressions remain in use.
Shared Skia deprecation/nullability warnings remain; no desktop interaction,
audible/native gameplay, or macOS/Linux execution is claimed by this feature check.

## Face build and artwork editing

Image-based Face recipes now have an interactive source registration view and a
Correction Input calibration view. Drag corners, place/move/remove pixel or area
samples, and manage same-colour groups. Apply and rebuild commits native recipe
history. Saved Panel2D sources retain the numeric recipe editor.

Face Build regenerates configured artwork, normalized lamp masks and native
trays/emitters as one document history entry. Runtime Build additionally invokes
the production standalone texture exporter; components requiring Machine composition
must use the existing Machine build. Build does not save the authored manifest.
Failures and cancellation restore the generated tree and document history. External
package/generated changes, links, and outputs outside the bound generated package
are refused. Lamp and component edits invalidate their dependent build states.

Authored lamp-mask import keeps an immutable package source and builds a separate
normalized generated mask. Source switching restores the linked saved Panel2D
artwork or mask. Component rebuild explicitly confirms replacement of local
component edits, preserving artwork and illumination. Current Base artwork can
be copied to an override; overrides can be enabled, disabled or removed through
history. Panel inspectors additionally expose asset paths, background colours and
Label lamp/text properties through the native session and MCP.

`capture_face_artwork_editor` renders the actual registration/calibration dialog
in the isolated MCP host, without native windows or desktop capture.

## Project launcher and input diagnostics

Desktop startup offers New/Open and a recent-project launcher when no project is
restored or supplied with `--project`. Recents are confined to the selected workspace;
missing/outside entries retain an explanation rather than being opened silently.
Headless sessions never read personal recents. `configure_recent_projects` requires
an explicit workspace-local file and enables reproducible welcome captures.

The Machine Inspector reports duplicate keyboard shortcuts and duplicate visual
bindings through the same analyzer as WPF. It also reports unresolved visual GUIDs
against the current Panel2D/Face context, without inventing Machine input mappings.
`inspect_machine_input_diagnostics` exposes the same diagnostics through MCP.

## Output and project package actions

The Output tab beside Project retains bounded status/error entries, severity/text
filters, Copy and Clear. Desktop logs append under the editor's local Logs folder;
headless and floating editors do not create personal log files. Floating status
and errors also reach the owner's Output. Saved layouts created before Output
restore with the tab added beside Project, retaining their existing arrangement.

Project package context actions provide Reveal, Rename and recoverable Delete.
Open/floating/dirty packages, links, destination conflicts and stale fingerprints
are refused. Rename preserves manifest bytes/native IDs and refuses saved references
(including paths inside the package) that would require rewriting. Delete checks
external references and moves the complete package to Generated/Trash, with a recovery
path. This conservative rename policy deliberately does not rewrite dependent assets.
Reveal invokes the operating system only when explicitly selected by the user.

Reproduce the latest background loop with:

```powershell
python OasisEditor.Avalonia.Mcp/verify_port_gap_workflows.py --output <directory>
```

The combined changes pass 214/214 Avalonia tests and 1,449/1,449 WPF regressions.
All four MCP verifiers pass, including real Face runtime textures/manifests,
rollback, guarded package actions, welcome and actual calibration-dialog captures,
retained/floating assets and GPU scenes. Dark/Light images were inspected. Linux x64
and macOS arm64 framework-dependent publishing succeeds with no apphost; native OS
execution/packaging, dialogs, physical clipboard/focus/input and mixed-DPI behavior
remain unverified. Existing Skia warnings remain. Capsule/Mesh collider previews
and animated/skinned GLB evaluation are shared WPF/static-renderer limitations.
A source audit found no additional substantial unported WPF authoring workflow.

If a failed Face build cannot restore generated files (for example, another process
holds an output open), its recovery backup is retained and the error reports the
path. Applying registration immediately reloads the rendered calibration input.

## Compact workspace verification

The Project/Output dock keeps enough height for asset rows and creation actions at
960×640, including after restoring a schema-1 workspace. Output filters/actions wrap
within narrow panes. The main and floating 3D toolbars scroll horizontally so their
last visibility control remains reachable. Actual headless pointer/key tests are
separate from physical keyboard, native clipboard and OS focus verification.

## Release artifact and input verification

Background verification now also executes the Release-published Windows MCP host,
rather than relying only on development output. All existing MCP clients accept
`--dll <published-OasisEditor.Avalonia.Mcp.dll>`; the default remains the development
build. `verify_background_ui.py` adds 960×640 and 1920×1200 Dark/Light captures,
Unicode project/asset names, native GPU Reel rendering, and persisted floating
workspace/redocking checks. For example:

```powershell
python OasisEditor.Avalonia.Mcp/verify_background_ui.py --dll <published-host.dll> --output <directory>
```

Headless routed-key tests exercise Ctrl/Meta scene shortcuts and keep text editing
with the focused Inspector/Output field. They use the headless platform's virtual
input and clipboard; they do not establish native OS focus/clipboard behaviour.
Returning from a 3D asset invalidates the cached 2D inspector, so retained Panel/Face
controls are rebuilt and edits target the active document. Regressions verify both
rendered Inspector contents and a subsequent edit after returning from a Reel.

The complete Avalonia suite passes 224/224 tests. All five MCP clients pass against
the Release-published Windows host, including the Reel/Library branch and actual
GPU captures; compact, large and floating images were inspected. No desktop input,
desktop capture, Player launch, audible playback or credited play was used. The initial
Ubuntu WSL dependency inspection resolved Linux Skia/HarfBuzz libraries before .NET
was installed. See the native Linux verification below for subsequent execution.
macOS execution, native OS dialogs/window focus/clipboard and physical mixed-DPI
monitors remain unverified.

## Linux GLES depth clear

Mesa may export the desktop `glClearDepth` entry point while the active context is
OpenGL ES, where that call produces `GL_INVALID_OPERATION`. The offscreen renderer
now uses `glClearDepthf` for GLES contexts and retains the desktop call for CGL.
The depth/texture/transparency/picking/context-lifetime/reflection regressions run
on Linux as well as Windows, with backend-specific device assertions. The recovery
test that relies on Windows file-sharing locks is explicitly skipped on Linux.

## Native Linux WSL verification

Ubuntu 24.04 x64 now has the user-local .NET SDK 9.0.320/runtime 9.0.22 under
`/home/john/.dotnet`; no shell profiles or system .NET defaults were changed.
Tracked sources were copied to an isolated Linux build directory so native restore,
build and publish did not overwrite the Windows checkout's build state. The native
Linux suite passes 223 tests with one explicit Windows-only sharing-lock skip.
The six real GPU tests are included, rather than skipped as on earlier runs.
The focused Windows renderer/Face regressions still pass 18/18.

All five MCP verification clients pass against the natively built/published Linux
host, including production builds/exports, Face masks/calibration, package guards,
Library assets, independent floating editors, workspace restore, UTF-8 paths,
compact layouts and actual rendered images. Dark/Light Panel/Face and 3D images
were inspected. The GL device was Mesa 25.2.8 / OpenGL ES 3.2 on llvmpipe; this verifies
software-backed GL execution, not hardware acceleration. WSL emitted DRI3 fallback
warnings but the rendering/pixel assertions passed. The minimal WSL font set lacks
Japanese coverage in the Unicode stress fixture; names still round-trip correctly.

For a Linux checkout with .NET 9 available, reproduce without desktop windows:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
dotnet publish WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp -c Release -r linux-x64 --self-contained false -p:UseAppHost=false -o /tmp/oasis-linux-publish
python3 WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_background_ui.py --dll /tmp/oasis-linux-publish/OasisEditor.Avalonia.Mcp.dll --output /tmp/oasis-linux-captures
```

The other clients (`verify_mcp.py --reel-library`, `verify_remaining_workflows.py`,
`verify_floating_documents.py`, `verify_port_gap_workflows.py`) accept the same
`--dll` and `--output` options. GPU statistics are inspected after an actual capture;
scene loading alone does not guarantee the first rendered frame already exists.
No Player/native-emulation process, audible playback, OS window/focus/clipboard,
physical monitor/DPI or macOS verification is established by these headless checks.

## Hardware-backed WSL verification

The existing WSL GPU bridge exposes the NVIDIA GeForce RTX 3060 through Mesa D3D12.
No graphics drivers, device permissions, shell profiles or system-wide environment
settings were changed. Process-local `GALLIUM_DRIVER=d3d12` and
`MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA` selected the adapter for verification.
The actual MCP renderer was `D3D12 (NVIDIA GeForce RTX 3060) / OpenGL ES 3.1 Mesa
25.2.8`. Seven focused renderer/camera/reflection tests and all five MCP workflows
passed with these settings, including inspected Dark/Light captures and multiple
simultaneous floating graphics contexts. This establishes hardware-backed offscreen
3D rendering; native desktop windows, OS focus/input and physical monitors remain
separate checks. The installed driver still emits a DRI3 warning during probing;
renderer identity and successful rendered/pixel tests establish the selected path.

`verify_background_ui.py --expected-renderer "D3D12 (NVIDIA GeForce RTX 3060)"`
fails if the actual captured scene reports another renderer and writes successful
device/frame statistics to `renderer.json` beside its images. A deliberate llvmpipe
run was rejected by this guard. It prevents a hardware verification job from
silently counting software rendering as success. Renderer matching is optional so
ordinary headless checks can continue to use working software GL.

```bash
GALLIUM_DRIVER=d3d12 MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA \
python3 WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_background_ui.py --expected-renderer "D3D12 (NVIDIA GeForce RTX 3060)" --dll /tmp/oasis-linux-publish/OasisEditor.Avalonia.Mcp.dll --output /tmp/oasis-hardware-captures
```

Select an adapter appropriate for the machine; do not force the WSL D3D12 driver
for ordinary Linux desktop distributions that use other native graphics drivers.

## End-user packages without a separate .NET install

Developer builds and the earlier framework-dependent MCP packages require the .NET
runtime. An `.exe`/apphost alone does not imply that the runtime is bundled. The
repository previously had no self-contained desktop release profile; source alone
did not establish that an existing Windows binary was install-free. End users do
not need an SDK or development tools, even for a framework-dependent application.

Both the desktop Editor and its headless MCP host now have a machine-neutral
`SelfContained` publish profile. Specify the target RID and ship the entire folder
(or package it in an installer/archive), including native libraries, fonts and
component definitions. These profiles bundle the runtime, preserve reflection/JSON
metadata, and deliberately avoid trimming, Native AOT and single-file extraction.
Normal OS graphics/font libraries and compatible drivers are still required.

```bash
dotnet publish WindowsNetProjects/OasisEditor/OasisEditor.Avalonia -c Release -r linux-x64 -p:PublishProfile=SelfContained -o /tmp/oasis-editor-linux
# Use -r win-x64 for the Windows folder package.
dotnet publish WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp -c Release -r linux-x64 -p:PublishProfile=SelfContained -o /tmp/oasis-mcp-linux
python3 WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_background_ui.py --host /tmp/oasis-mcp-linux/OasisEditor.Avalonia.Mcp --output /tmp/oasis-self-contained-captures
```

Desktop folder publishing succeeded for Linux x64 and Windows x64. Matching
self-contained MCP apphosts passed the background UI and main Reel/Library,
authoring/build/export workflows on both systems with `dotnet` absent from PATH
and runtime-search roots pointed at nonexistent locations. Windows host tracing
confirmed that CoreCLR loaded from the published application folder. Linux also
retained its verified RTX 3060 renderer. Native desktop launching was not performed
while the user was working. Bundled-runtime patches ship through application
releases, so release builds should use an up-to-date supported SDK/runtime.

Native AOT is a separate optimization/compatibility project; self-contained publishing
already avoids a separate runtime installation. Avalonia supports AOT, but this Editor has unverified
reflection-based JSON/native settings and third-party docking/control metadata.
Oasis scripting itself is an interpreter, with no runtime assembly/code generation
found in the audited implementation. AOT has not been published or validated here;
it must preserve all authoring/save/load workflows before becoming a release mode.

Deployment references: [Microsoft publishing modes](https://learn.microsoft.com/en-us/dotnet/core/deploying/) and [Avalonia Native AOT](https://docs.avaloniaui.net/docs/deployment/native-aot).
