# Current Priority for Codex

The active user-authorised workstream is the staged Avalonia Editor migration,
with Unity-inspired styling and background MCP authoring/capture. The user has now
explicitly authorized Avalonia 3D authoring and scene preview.
The parallel application now creates real projects, imports FML/bitmap artwork,
authors Panel2D/source regions, generates/processes native Faces, renders specialist
2D components, edits Machine settings, builds/exports Player packages, previews real
Fabric/Amber outputs, persists the workspace and includes Cabinet3D/Prefab/Machine
scene authoring with a static GPU preview. See `OasisEditor.Avalonia/README.md` for
the supported 3D content, MCP surface and platform verification limits. Multiple authored assets are retained in tabs with document-local history,
save/conflict guards and clean workspace restoration. The user has additionally authorized the remaining
Reel and Library workflows. Native physical Reel authoring/preview and Library
browsing, shared editing, package copying and scoped asset choices are implemented.
Face reel-mount symbol-band/lamp/mask authoring is also implemented through the UI
and MCP, with native save/history and real-asset background capture verification.
Additional Panel/Face component, clipboard, source-corner, generation-settings,
project-close/Face-validation, Machine composition graph and processing/Player
preferences workflows are now available through Avalonia and MCP. WPF remains
available. Individual asset tabs now float into independent editors and redock
without saving or discarding pending edits/history. Workspace persistence includes
their bounds, panes, selection and cameras. Face Build/illumination/source switching, interactive image registration/calibration,
override lifecycle and remaining Panel asset/Label inspector fields are also available.
Recent-project welcome, live input diagnostics, Output logging/filtering and guarded
project package context actions are also ported. Current combined validation: 224
Avalonia tests and 1,449 WPF regressions; five Release-published background MCP verifiers and inspected
Dark/Light images. Native Linux WSL builds/publishing now pass with 223 tests and
one explicit Windows-sharing skip; all five MCP workflows and inspected UI captures
pass on Mesa GLES/llvmpipe. Native desktop, hardware-accelerated Linux and macOS
verification remain separate work. Preserve its
working authoring features while extracting shared services. Do not substitute
mock data or scripted emulation previews for real systems.

The preceding runtime workstream is real Fabric / Amber JPM Impact integration
in Unity Player, using the Editor as reference. See
OasisEditor.Mcp/PLAYER_FABRIC_IMPACT.md and the arcade plan.
Native boot, output/audio/input delivery, reset/restart and the mixed Pool/Impact
arcade passed actual local verification. The user has no current saved project with
verified coin/Start mappings and will prepare one later; credit/spin/payout tests
are deferred until then. Do not guess those settings or claim credited play.

Walkable preview and arcade composition are merged in PRs #748/#749. Current
Machine authored/runtime schemas are 11/13 (Prefab 2). The original Pool build was
regenerated without changing its authored gameplay. Editor tests pass 1,445/1,445;
focused Unity tests pass 220/220. Manual visual/audio/keyboard checks remain excluded
while the user is working. The installed JPM DLL supports one active board per
process; it may run alongside Oasis machines, but not another simultaneous JPM board.

P1 authoring is delivered in PR #745; P2 runtime components and the P3 single-ball
checkpoint are merged in PR #746. The Pool-required P4 lifecycle/language/query
subset and P5 calculated Pool setup are merged in PR #747 and locally verified.
Optional values and physics-step kinematic targets remain deferred with P6–P8.
The original PoolTableTest1 project was backed up, explicitly reauthored and
verified in the actual Player with sixteen runtime nodes. The user authorised this sequence.

For resumed Player/runtime work, read AGENTS.md, then Docs/ArchitectureNext/00_OVERVIEW.md,
10_ACTIVE_ROADMAP.md, 13_PREFABS_OASISOBJECTS_AND_COMPONENTS.md and
14_PREFAB_AND_POOL_DELIVERY_PLAN.md. For baseline details read relevant sections of
11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md, 12_OASIS_SCRIPT.md and
09_RUNTIME_BUILD_AND_PLAYER_CONTRACT.md; inspect current source before each phase.

Delivered baseline: A1–A7 and A8.1–A8.4; A8.5 initial single-ball Pool script and
integration coverage, merged trigger discoverability and authored development
keyboard bindings. A disposable actual Pool copy with corrected anchors passed
single-ball physics, all six natural pocket drops, input/trigger/reset on the P1
runtime baseline and again under P2 with a parented ball and inline child. The calculated 15-ball rack plus separate cue also passed actual Player physics,
all six natural pocket drops, collection/recovery, repeated new games and unload/reload.
Preserve the single-ball fixture as regression coverage. Spawning, bounded functions,
typed lists/live refs and focused geometry queries are implemented.

Gameplay remains Machine-owned behavior.oasis. Preserve A7 commands/timers and
A8.4 startup/fault/unload semantics; never attach behaviour to Emulation Machines.
Proposed A8.6 fruit-device work is deferred behind this track; A8.7 mole validation
is expanded into P7, with a moving-shelf/coin proving case at P8. Variants are P6,
a separate later milestone, not a Pool prerequisite. No parallel competing plans.

Restricted cloud agents must not execute unsupported builds/tests. Local agents may
use supported .NET/Windows and Unity tools under AGENTS.md Environment Constraints.
No application/schema/asset/generated-output
changes are authorised by this planning document itself.
