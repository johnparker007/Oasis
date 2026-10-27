# Oasis Editor MCP

`OasisEditor.Mcp` is the canonical local stdio MCP host for Oasis Editor. It runs
headless authoring and inspection workflows against the production Editor services.
It uses an explicit workspace and does not attach to, activate or send input to a
visible desktop application. The retired WPF MCP host is not a supported entry point.

## Build and connect

From the repository root with the .NET 9 SDK:

```powershell
dotnet build WindowsNetProjects/OasisEditor/OasisEditor.Mcp
dotnet run --project WindowsNetProjects/OasisEditor/OasisEditor.Mcp -- --workspace <existing-directory>
```

Configure your MCP client to launch the built host with `--workspace` and an existing
project workspace. Keep stdout available for the MCP protocol. Save authored changes
explicitly; session history is not a replacement for persisted project files.

## Authoring and inspection

The host exposes workflows for project creation/opening; Panel2D and FML import; Face
creation, generation and artwork processing; Machine settings and input editing;
Cabinet and Prefab scene authoring; Library asset selection; workspace save/restore;
undo/redo; and structured inspection. Capture operations return images rendered by
the application backend without opening OS windows or capturing the desktop. The
production build/export tools use `Oasis.RuntimeBuild`.

Preview operations use the real Fabric/Amber backend and explicit provider libraries.
They do not invent game mappings or treat boot/output activity as proof of credited
play. Use a disposable project for authoring checks. Build/runtime checks, native
emulation, rendered appearance, audible playback and physical desktop interaction
are distinct forms of verification.

For app behavior and workspace usage, see [`OasisEditor/README.md`](../OasisEditor/README.md).
For historical native Player integration details, see
[`Docs/NativeIntegration`](../Docs/NativeIntegration/).
