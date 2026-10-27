# Agent project context

Recorded on 2026-10-27 from the user's project handoff and existing priority document. The canonical Editor cutover was documented afterward. This is a starting point for investigation, not a fresh verification report. Update changing status here or in the current priority document rather than duplicating it in agent rules.

## Source documents

- Current workstream: `WindowsNetProjects/OasisEditor/00_CURRENT_PRIORITY.md`.
- Historical Fabric/Amber integration: `WindowsNetProjects/OasisEditor/Docs/NativeIntegration/PLAYER_FABRIC_IMPACT.md`.
- Architecture entry point: `WindowsNetProjects/OasisEditor/Docs/ArchitectureNext/00_OVERVIEW.md`.

Paths above are relative to the repository root. Follow the current priority document for relevant roadmap and runtime-contract sections.

## Existing work to inspect before extending

- The canonical Editor (`WindowsNetProjects/OasisEditor/OasisEditor`) and MCP host (`OasisEditor.Mcp`) support substantial authoring, inspection, export and capture operations.
- Shared services are organized into `Oasis.Authoring.Core`, `Oasis.Emulation`, `Oasis.Rendering2D`, `Oasis.RuntimeBuild` and `Oasis.Rendering3D`. The former WPF Editor and WPF MCP host are retired.
- PoolTableTest1 uses Oasis scripts and a ball prefab to generate a rack of 15 balls plus a cue ball.
- Player has basic arcade placement, walking, machine interaction profiles, and pool aiming/shooting.
- Player supports exported Fabric configuration and the Amber JPM Impact core, using the Editor implementation as the reference.
- Real ROM execution and a mixed Pool/Impact arcade were checked in background tests. These earlier checks do not establish the current build's visual or audio correctness.

## Local authored content

The user's main saved pool project is:

`C:\Users\John\Documents\Oasis\Editor\Projects\PoolTableTest1\PoolTableTest1.oasisproj`

This path is machine-specific. Confirm it exists and inspect the project before use; preserve and back up authored data in accordance with the root guidance.

## Historical emulation constraints and pending verification

At the initial integration:

- The installed Amber Impact provider supported one active Impact board per Player process.
- Player retained all 512 lamp channels, while the Face visual format supported lamp indices 0–254.
- Native execution checks did not establish correct rendered appearance or audible playback.
- Actual credit, spin, and payout verification awaited a current Editor project with working ROM, coin, and Start configuration. The user intended to create that project; older incompatible projects were not ready for that testing.

Verify whether these limitations and the pending test prerequisite still apply before planning around them. Do not infer game-specific coin/Start mappings from this handoff.
