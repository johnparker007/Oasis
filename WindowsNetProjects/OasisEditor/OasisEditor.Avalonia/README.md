# Avalonia Editor migration

This is the first functional slice of the parallel cross-platform Editor. The WPF
Editor remains the complete authoring application. No authored schema changes are
introduced, and no Player files are part of this migration.

## Delivered

- Unity-inspired compact workspace with real Dock tool/document panes: Hierarchy,
  Inspector, Project and Panel2D. Pane movement/floating uses Dock; real desktop
  interaction and multi-monitor behaviour still require manual verification.
- System, Dark and Light themes using semantic resources.
- Open an existing schema-9 project with the standard Assets/Generated layout.
  Discover, open and create current schema-3 Panel2D packages.
- Skia-backed Avalonia 2D artwork/geometry/text viewport with fit, wheel zoom,
  middle-button pan, picking and undoable drag movement. The inspector edits name,
  position, dimensions, visibility and transform locking through the same commands
  used by automation. A panel can contain all current element DTO fields, but this
  viewport does **not** reproduce specialist reel/lamp/display visual behaviour.
- Document history, exact saved-state tracking, dirty-replacement protection,
  atomic save and external-manifest-change detection. Switching saved panels retains
  their histories while their persisted contents match. One panel is active at a time.
- The existing Panel2D storage/model sources now live in `Oasis.Authoring.Core`,
  consumed by both Editor implementations. There is no WPF dependency in that core
  or either Avalonia project. Existing WPF storage behaviour is unchanged.
- New isolated stdio MCP host using the same authoring session and real workspace
  controls. Capture uses Avalonia Headless with the Skia renderer; it creates no OS
  windows and never reads the desktop or sends system mouse/keyboard input.

The new session accepts current schemas only and rejects unrecognized Panel2D
properties rather than silently dropping them. Full current DTO data is preserved
through edits; tests cover native metadata, reel settings and source shapes.
Workspace/project paths are confined, separator-normalized and checked for links.
Use a disposable project copy for development; image assets are read-only.

## Build and run

From the repository root with .NET 9 SDK installed:

```powershell
dotnet build WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp
dotnet test WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Tests
```

Only run this next command when a visible application is wanted:

```powershell
dotnet run --project WindowsNetProjects/OasisEditor/OasisEditor.Avalonia -- --workspace <projects-directory> --project <project.oasisproj>
```

Project paths must be inside the explicit workspace. Relative project paths in
the MCP session resolve against that workspace. Double-click a panel in Project
to open it. Save or undo pending changes before switching panels/projects or
closing. Ctrl/Cmd+S saves; Ctrl/Cmd+Z and Ctrl/Cmd+Shift+Z undo/redo outside text
fields. The initial layout has no persisted workspace preferences yet.

## Background MCP loop

Launch the separate host with:

```text
dotnet <absolute-path>/OasisEditor.Avalonia.Mcp.dll --workspace <existing-directory>
```

Tools: `open_project`, `inspect_workspace`, `open_panel2d`, `create_panel2d`,
`add_rectangle`, `select_element`, `update_element`, `history`, `save_panel2d`,
`capture_workspace`. Captures return PNG image content; dimensions are bounded to
960–1920 by 640–1200. Themes are System/Light/Dark. This host is independent of the
existing WPF MCP server and does not change the user's configured MCP connections.

The standard-library Python client tests the actual stdio protocol end to end:

```powershell
python WindowsNetProjects/OasisEditor/OasisEditor.Avalonia.Mcp/verify_mcp.py --output <capture-directory>
```

It authors a disposable test fixture, edits elements, checks undo/redo and
save/reopen, and captures Dark, Light and compact workspaces. Optional
`--project <existing.oasisproj> --panel <asset.panel2d>` adds a read-only capture of
real authored content. The small generated project is a **test fixture**, not a
replacement for native project creation/scaffolding. Do not commit captures or
private project assets.

## Remaining migration work

Continue with full project scaffolding and asset browser workflows, additional
Panel2D authoring/selection and source-shape tools, FML import and Face authoring,
specialist 2D renderers, workspace layout persistence, then build/export and
emulation service extraction. Keep the real Fabric/Amber path. 3D is explicitly
deferred until its renderer has been selected and proved cross-platform.

Verified locally on Windows: new projects build without warnings; portable session
and headless UI tests; actual MCP authoring and image captures; read-only capture of
an existing 196-element panel; existing WPF regression suite (1,448 tests). The first
WPF run had an audio timing assertion failure; its focused rerun and full rerun
passed. A WPF rebuild reports 332 existing compiler warnings.

Not yet verified: macOS/Linux execution and packaging, native desktop docking,
floating windows, OS clipboard/dialogs, real pointer/keyboard focus, mixed-DPI
monitors, specialist rendering parity, audio or native emulation in Avalonia.
Headless screenshots establish rendered layout in that backend, not these desktop
or platform behaviours.
