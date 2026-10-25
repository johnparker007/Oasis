# Oasis Editor MCP

An opt-in, local **stdio MCP server** using the official C# SDK. It runs an isolated
editor automation session on a WPF STA dispatcher. It never attaches to an existing
editor, activates a user window, or opens the normal launcher.

The first slice supports a repeatable project → Panel2D → MFME FML → save/reopen
workflow. It reuses the editor's project scaffolder, importer, document command
history, validation and save service. It does not implement Face/Cabinet/Machine
assembly or runtime export yet. Existing ArchitectureNext priorities are unchanged.

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
| `save_panel2d` | Save the session document, rejecting external manifest changes. |
| `undo`, `redo` | Apply the document's existing command history. |
| `capture_panel2d` | Return a native MCP PNG image of the actual WPF Panel2D edit view. |

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
Import warning/error and copied-file lists are bounded to the first 100 entries;
`copiedAssetCount` gives the full copied-file count.

## Capture behavior

`capture_panel2d(documentId, width=1000, height=700, theme="System")` supports System,
Light and Dark. Width is limited to 320–1920 and height to 240–1080 at 96 DPI.
It returns the real `SkiaPanel2DEditView`, including its viewport/status controls,
rendered through a non-activating offscreen tool window. The presentation source is
necessary for Skia's WPF element; a disconnected visual can otherwise omit artwork.
Project-relative images resolve against the active project during capture.

This is **a view render**, not a desktop screenshot or complete editor-window grab.
It excludes shell chrome, menus, native dialogs, popups and unrelated windows.
It uses the current viewport (initially 100%); content outside it is clipped as in
the editor. A Windows desktop-capable environment is still required. Rendered
images can support visual checks but do not establish mouse/keyboard usability.

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

Session behavior tests are in `OasisEditor.Tests/McpEditorSessionTests.cs`:

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
With `--fml` it also verifies nonempty import and undo/redo counts. No FML, ROMs,
imported user content or screenshot artifacts are stored in this repository.
