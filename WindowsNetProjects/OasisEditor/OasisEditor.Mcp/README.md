# Oasis Editor MCP

An opt-in, local **stdio MCP server** using the official C# SDK. It runs an isolated
editor automation session on a WPF STA dispatcher. It never attaches to an existing
editor, activates a user window, or opens the normal launcher.

The server supports a repeatable project → Panel2D → MFME FML → edit → Face → save/reopen
workflow. It reuses the editor's project scaffolder, importer, document command
history, validation and save service. It does not implement Cabinet/Machine
assembly or explicit runtime-export tools yet. Face generation and saving reuse
the editor's existing derived-asset/export behavior. ArchitectureNext priorities
are unchanged.

## Build and connect

Requires Windows, .NET 9 SDK and WindowsDesktop runtime/reference pack.
From `WindowsNetProjects/OasisEditor`:

```powershell
dotnet build OasisEditor.Mcp/OasisEditor.Mcp.csproj -c Release
```

Create a dedicated automation workspace outside the source tree. Configure an MCP
client to launch the built executable with **both** arguments:

```text
OasisEditor.Mcp.exe --workspace C:\OasisAutomation
```

Example Codex `config.toml` entry (replace paths for your checkout):

```toml
[mcp_servers.oasis_editor]
command = 'C:\path\to\OasisEditor.Mcp\bin\Release\net9.0-windows\OasisEditor.Mcp.exe'
args = ['--workspace', 'C:\OasisAutomation']
tool_timeout_sec = 120

[mcp_servers.oasis_editor.env]
WINDIR = 'C:\Windows'
SystemRoot = 'C:\Windows'
```

The workspace must exist. Preserve the normal Windows environment: WPF font
initialization fails if the host strips required Windows shell/font variables.
The Python smoke client explicitly inherits its parent's environment for this reason.
Protocol messages use stdout; legacy importer diagnostics and host logs use stderr.
Closing the client terminates the isolated session. Save before closing: unsaved
changes and undo history are session-local, and no confirmation dialog is shown.

## Tools

| Tool | Purpose |
| --- | --- |
| `create_project` | Create a new project by name; existing folders are refused. |
| `open_project` | Open a current-schema project with the standard Assets/Generated layout. |
| `inspect_project` | Read the project path and open document IDs. |
| `create_panel2d` | Create and initially save a fresh Panel2D asset package. |
| `open_panel2d` | Validate/reopen a persisted package and obtain a new session document ID. |
| `import_fml` | Import a workspace FML through the document's undoable import command. |
| `inspect_panel2d` | Read dirty state, undo/redo availability, total element count and a page of elements. |
| `inspect_panel_element` | Read a single element by ID, including visibility, lock and selection state. |
| `select_panel_elements` | Replace transient selection by stable IDs; an empty array clears it. |
| `update_panel_element` | Atomically edit supported properties through one undoable editor command. |
| `set_panel_view` | Set transient zoom and pan without dirtying the document. |
| `save_panel2d` | Save the session document, rejecting external manifest changes. |
| `undo`, `redo` | Apply the document's existing command history. |
| `capture_panel2d` | Return a native MCP PNG image of the actual WPF Panel2D edit view. |
| `add_face_source_shape` | Add an undoable convex source quadrilateral; defaults to the whole panel. |
| `inspect_face_source_shapes` | Page through persisted/in-memory source-shape IDs and corners. |
| `create_face` | Create and initially save a new blank or image-based Face. |
| `generate_face_from_panel` | Generate a new unsaved Face from a saved panel/source shape. |
| `open_face`, `save_face` | Reopen/persist a Face through the existing validation/save service. |
| `inspect_face` | Read source links, authored/generated paths, semantic elements and build diagnostics. |
| `capture_face` | Return the actual WPF/Skia Face layout view fitted offscreen. |

Start with `create_project(name="MCPTest1")`, then `create_panel2d(name="Main Panel")`.
Use the returned `documentId` for subsequent calls. Copy an FML into the automation
workspace and pass its absolute or workspace-relative path to `import_fml`.
Inspect `succeeded`, errors and warnings before saving. A failure is returned as
structured import diagnostics, not as an apparently successful import. Invalid
arguments and other operation failures are MCP tool errors.

Panel manifests use the current package convention:
`Assets/Panel2D/<Name>/asset.panel2d`. Only one project can be opened per MCP process;
connect a new session to switch projects. Reopening yields new document IDs.
Inspection defaults to 50 elements and accepts a maximum of 200 per page.
Panel snapshots also include `selectedElementIds` and `view` (zoom/pan). Element
snapshots include `isVisible`, `isTransformLocked` and `isSelected`.
Import warning/error and copied-file lists are bounded to the first 100 entries;
`copiedAssetCount` gives the full copied-file count.

## Element editing

`update_panel_element` takes `documentId`, `elementId` and an `edit` object:

```json
{"documentId":"<session document ID>","elementId":"<element ID>","edit":{"name":"Prize display","x":120,"y":80,"width":160,"height":40,"isVisible":true}}
```

Supported fields are `name`, `x`, `y`, `width`, `height`, `isVisible` and
`isTransformLocked`. Omitted or null fields remain unchanged. A combined update is
one undo step; invalid fields reject the entire edit. Same-value updates add no
history, do not dirty a clean document and preserve redo. Names must not be blank;
positions must be finite and dimensions positive and finite. Transform-locked
elements require a separate unlock call before geometry changes, even if the
proposed geometry edit also requests unlocking. Rename/visibility changes are
allowed while the transform is locked. IDs, types and asset paths cannot be
rewritten through this tool. Edits do not auto-save.

`select_panel_elements` takes an array `elementIds` (maximum 200). All IDs are
validated in the specified document before selection changes. Selection is visible
in captures but never stored as authored content or recorded in undo history.

`set_panel_view` takes `zoom` (0.01–64, where 1 is 100%) and optional `panX`/`panY`
(default zero) in view coordinates. These settings are transient and do not add
undo history or dirty the document.

## Capture behavior

`capture_panel2d(documentId, width=1000, height=700, theme="System")` supports System,
Light and Dark. Width is limited to 320–1920 and height to 240–1080 at 96 DPI.
It returns the real `SkiaPanel2DEditView`, including its viewport/status controls,
rendered through a non-activating offscreen tool window. The presentation source is
necessary for Skia's WPF element; a disconnected visual can otherwise omit artwork.
Project-relative images resolve against the active project during capture.

This is **a view render**, not a desktop screenshot or complete editor-window grab.
It excludes shell chrome, menus, native dialogs, popups and unrelated windows.
Use `framing="current"` (default) for the session viewport, `framing="fit"` to fit
the panel, or `framing="element"` with `elementId` to centre and fit an element with
a small margin. Element framing uses its geometry even when it is hidden; hidden
artwork remains hidden. Fit uses the editor's existing logical bounds/background
and image-pixel scaling. Zoom limits still apply to extremely small/large elements.
Captures restore the previous zoom/pan afterward and never dirty the document.
Content outside the chosen viewport is clipped as in the editor. A Windows
desktop-capable environment is still required. Rendered
images can support visual checks but do not establish mouse/keyboard usability.

## Face authoring

`create_face(name, sourceImage?)` follows the editor's native workflow. Omitting
`sourceImage` creates a blank Face; an image path must be inside the configured
workspace and have dimensions from 1 to 4096 pixels per axis. The new package is
initially saved. Existing authored or generated packages are refused.

For a derived Face:

1. Import/open the source Panel2D.
2. Call `add_face_source_shape(panelDocumentId, name, corners?)`. Without corners it
   uses the editor's whole-panel logical bounds. A custom `corners` object contains
   `topLeft`, `topRight`, `bottomRight`, `bottomLeft`, each with numeric `x`/`y`.
   Ordered convex, nondegenerate quadrilaterals are required. Corners must be finite
   and within ±1,000,000; estimated output is limited to 4096 pixels per axis.
3. Use `inspect_face_source_shapes` to retrieve IDs/corners. Adding a shape uses the
   existing panel command and supports Panel2D undo/redo. Undoing a saved addition
   correctly marks the source panel dirty.
4. Save the source panel. Generation rejects dirty sources and externally changed
   source manifests so persisted source links describe the actual source state.
5. Call `generate_face_from_panel(panelDocumentId, shapeId, name)`. It reuses the
   editor's perspective correction, artwork processing, mask extraction, semantic
   component conversion and tray/emitter authoring services. Without a selected
   cabinet target it derives output aspect from the source quad. The response
   includes the new `face` snapshot and conversion counts.
6. The new Face is initially **unsaved**. Call `save_face(face.documentId)` to create
   its authored package, then inspect build-product statuses and errors. Saving an
   authored Face does not guarantee every derived runtime product is current.
7. `capture_face` returns the real fitted Face layout view, without building or
   saving. It supports the same size limits and themes as panel capture and
   restores its viewport. `open_face` reopens persisted content in a new session.

Face snapshots distinguish the persistent `assetId` from the session `documentId`,
include source panel/shape links, bounds, tray/emitter counts and paged semantic
elements, and expose build-product statuses/errors. `artworkPath` and `maskPath`
can refer to derived/intermediate output; `authoredArtworkPath` and
`authoredMaskPath` identify the canonical authored files in the package (which may
not exist until a generated Face is saved). `inspect_project` lists open Faces as
well as open panels. `inspect_face` defaults to 50 elements, maximum 200 per page.

This slice creates new Faces; it does not regenerate/overwrite an existing Face,
edit Face components or provide workspace-wide undo. Panel `undo`/`redo` covers
source-shape edits but does not undo Face creation or filesystem writes. Generation
observes cancellation where the underlying generator supports it. Failures or
cancellation may leave generated/intermediate files; no rollback/delete is promised.

Face saves compare fingerprints of **all authored package files**, including the
manifest and PNGs, to detect external additions/changes/deletion. Changes to
disposable `Generated/` previews are not conflicts. New output/image paths and
persisted Face asset references are restricted to the workspace, with junctions
and symlinks refused. Use disposable projects for initial automation.

## File behavior and boundaries

- Use disposable workspaces until a workflow is verified. New project and package
  paths are never deliberately overwritten. A SHA-256 check protects open panel
  manifests from being overwritten after external edits or deletion.
- Explicit project, panel and FML paths must remain inside the configured workspace.
  Junctions/symbolic links are rejected, including existing import destinations.
  Panel image references are checked before opening/capture.
- This is local automation, not an OS security sandbox. The existing importer also
  writes staging/diagnostic files under its normal application locations. Do not
  expose the stdio process as an unauthenticated remote service.
- Import may copy assets before reporting a failure. The result exposes copied
  paths/counts when available. Neither failed import nor document undo deletes
  those files, and undo does not reverse a save. There is no transaction/rollback
  promise across filesystem writes. Existing importer collision handling remains
  in use; no generic delete or arbitrary-code tool is exposed.
- Calls execute sequentially on the dispatcher. Cancellation can prevent queued
  work; an already running synchronous import is not preempted. Long jobs, progress
  polling, live-editor attachment and full-shell capture are future work.

## Verification

Session behavior tests are in `OasisEditor.Tests/McpEditorSessionTests.cs` and
`McpPanelEditingTests.cs`, plus `McpFaceAuthoringTests.cs`:

```powershell
dotnet test OasisEditor.Tests/OasisEditor.Tests.csproj -c Release
```

The independent Python MCP smoke client needs Python 3.10+ and `fastmcp>=3,<4` in a
development virtual environment. It creates/removes its own temporary workspace;
server logs and captures remain in the supplied artifacts directory.

```powershell
python OasisEditor.Mcp/smoke_test.py --server C:\absolute\path\OasisEditor.Mcp.exe --artifacts C:\OasisSmokeResults
# Optional real import verification, using a local FML that is copied, never changed:
python OasisEditor.Mcp/smoke_test.py --server C:\absolute\path\OasisEditor.Mcp.exe --artifacts C:\OasisSmokeResults --fml C:\Layouts\Example.fml
```

The smoke checks tool discovery, path rejection, create/save/inspection, PNG capture
in three themes, invalid-FML diagnostics and persisted reopening in a fresh process.
With `--fml` it also verifies nonempty import, element selection/editing, undo/redo,
fit and element framing, viewport restoration and persistence of edited properties.
`check_headless_startup.py --server <exe>` separately checks that startup/idle opens
no visible windows and the process exits on stdin EOF; it uses only Python's
standard library and sends no desktop input. No FML, ROMs,
imported user content or screenshot artifacts are stored in this repository.

`face_smoke_test.py` exercises the complete source-shape/generation/save workflow,
blank/image-based creation, bounded inspection, three-theme Face captures,
cross-process reopen, duplicate refusal and external manifest conflict handling:

```powershell
python OasisEditor.Mcp/face_smoke_test.py --server C:\absolute\path\OasisEditor.Mcp.exe --artifacts C:\FaceSmokeResults --fml C:\Layouts\Example.fml
```
