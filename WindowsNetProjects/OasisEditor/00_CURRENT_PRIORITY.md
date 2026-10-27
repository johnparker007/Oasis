# Current Priority for Codex

The canonical Editor is the portable Avalonia application at
`WindowsNetProjects/OasisEditor/OasisEditor`. Its MCP host is
`OasisEditor.Mcp`, and its test project is `OasisEditor.Tests`. The former WPF
Editor and WPF MCP host are retired. Historical native-integration references are
kept under `Docs/NativeIntegration/`; they do not describe supported application
entry points.

Shared authoring and runtime services live in `Oasis.Authoring.Core`,
`Oasis.Emulation`, `Oasis.Rendering2D`, `Oasis.RuntimeBuild` and
`Oasis.Rendering3D`. Preserve authored schemas and Player contracts unless the user
explicitly authorizes a change. Inspect the canonical application and relevant
shared project source before deciding how to extend a workflow.

The Editor covers Panel2D, Face, Reel, Cabinet3D, Prefab and Machine authoring;
artwork import and processing; native Fabric/Amber preview; Player build/export;
workspace persistence; and headless MCP authoring and inspection. Continue building
complete end-to-end workflows on the real production services. Do not substitute
mock data or scripted gameplay previews for native emulation.

For ongoing Player/runtime work, read `Docs/ArchitectureNext/00_OVERVIEW.md`,
`10_ACTIVE_ROADMAP.md`, `13_PREFABS_OASISOBJECTS_AND_COMPONENTS.md` and
`14_PREFAB_AND_POOL_DELIVERY_PLAN.md`. For baseline behavior, consult the relevant
sections of `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`, `12_OASIS_SCRIPT.md`
and `09_RUNTIME_BUILD_AND_PLAYER_CONTRACT.md`, then confirm details against current
source. Gameplay remains Machine-owned `behavior.oasis`; preserve startup, fault,
unload and lifecycle semantics, and do not attach behavior to Emulation Machines.

Historical native Fabric/Amber Impact integration notes are in
`Docs/NativeIntegration/PLAYER_FABRIC_IMPACT.md`. Earlier verification said the
installed JPM DLL supports one active Impact board per Player process, and that
credit/spin/payout checks awaited a current project with verified ROM, coin and
Start settings. Treat those statements as historical prerequisites and re-check
current source and saved project state before relying on them. Do not guess game
input mappings or claim credited gameplay without direct evidence.

This document is guidance, not authorization for unrelated application, schema,
asset or generated-output changes. Follow the repository `AGENTS.md` and Player
`AGENTS.md` for preservation, environment and verification requirements.
