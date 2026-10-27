# Oasis agent guidance

Oasis is a machine authoring environment and playable virtual arcade. The main applications are:

- `WindowsNetProjects/OasisEditor`: Windows authoring application, including an MCP server for automated authoring and inspection.
- `UnityProjects/OasisPlayer`: Unity application for running authored machines in a walkable arcade.

## Start here

Inspect Git status and read the applicable nested `AGENTS.md` before changing files. Preserve existing user changes; never reset files or overwrite unrelated work.

Read `WindowsNetProjects/OasisEditor/00_CURRENT_PRIORITY.md` for the current workstream, then only architecture documents relevant to the requested task. Do not scan every planning document. For Player work, also read `UnityProjects/OasisPlayer/AGENTS.md`. For Editor work, also read `WindowsNetProjects/OasisEditor/AGENTS.md`.

Current integration context and local test-project details are in `Docs/AGENT_PROJECT_CONTEXT.md`. Treat status statements there and in priority documents as historical evidence to verify against current source, rather than guarantees about today's implementation. A priority document does not independently authorize unrelated changes.

## Product direction

Build a functional arcade where players can walk around, approach machines, and use the appropriate interaction mode. The Editor should author complete machines through both its interface and MCP: projects, panels, imported layouts, assets, prefabs, scripts, interaction settings, and emulation configuration.

Build functional end-to-end features first, then refine them. Avoid temporary demonstrations that bypass the real systems. Develop meaningful MCP authoring and structured inspection alongside features where useful. MCP checks complement visual testing; they do not establish correct rendering.

For fruit machines, use the real Fabric and Amber emulation path. Do not substitute scripted attract lamps or simulated credit, spin, or payout behavior. Use the existing Editor integration as the reference for configuration, inputs, lamps, reels, displays, audio, and lifecycle. Do not guess game-specific mappings or treat incompatible old projects as ready for testing.

## Protect the user's PC and content

The user often works on this PC during development. Prefer source changes, command-line tools, MCP, automated tests, and background verification. Do not open or repeatedly restart visible applications, activate windows, change focus, or take over mouse or keyboard input without explicit authorization.

Manual layout inspection and mouse/keyboard interaction tests require explicit authorization. Ask before desktop capture or other intrusive UI work. Finish independent background work first and report any visual, audio, or manual checks that remain unverified.

Do not stop user-owned Editor, Unity, or Player sessions. Identify processes and ownership before terminating anything. Inspect saved projects before modifying them and back up authored data before consequential changes. Ask before changing an open project when unsaved work may be affected.

## Implementation and verification

Follow repository conventions and keep changes focused. Respect each application's supported language version and API surface. Do not change Unity packages or configuration merely to experiment with automation without approval.

Use the smallest appropriate set of meaningful checks; expand when failures or new changes justify it. Distinguish source/build validation, automated runtime checks, real native emulation checks, and visual/audio/manual interaction checks. Never describe an unperformed check as passed. Native execution alone does not establish rendered appearance or audible playback. Report material limitations and unrelated existing failures accurately.

## Standing GitHub permissions

Agents working on Oasis have standing user permission to create branches, commit, push, open pull requests, and merge completed work in this repository. This permission applies to future agents and, when delegation is permitted, delegated agents working within the authorized task scope. Do not repeatedly request approval for these actions.

Use `codex/` branch names. Review final changes and run appropriate checks before merging. Follow required repository reviews and CI rules; do not bypass them. Complete the GitHub workflow when feasible rather than leaving finished work only on disk. Exclude unrelated user changes, ROMs, private local assets, credentials, and generated verification debris. Report the PR or commit and whether it was merged.

## Cost and communication

The user's token allowance is limited. When delegation and model selection are available and permitted, use cheaper models or agents for bounded, straightforward tasks. Keep delegation purposeful; avoid duplicated research and repeated unchanged tests.

Work autonomously within the requested scope. Ask when a missing decision materially affects the result. Give concise progress updates during sustained work and identify concrete blockers when progress stalls. End with what changed, how it was verified, GitHub status, and remaining limitations or next steps, using plain language.
