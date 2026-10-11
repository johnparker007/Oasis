# Current Priority for Codex

P1 ordinary Prefab authoring is implemented in the current working branch, pending
Windows Editor/.NET and Unity verification, review and merge. Authored Object3D
schema 2 now has stable inline node/component IDs, document commands, hierarchy,
Inspector and source previews. One asset family retains asset.object3d and its
Project/Library package paths. Runtime Object3D schema 1 and the Player remain
unchanged; explicit admission rejects data they cannot represent. See
Docs/ArchitectureNext/15_P1_PREFAB_AUTHORING_VERIFICATION.md for format
reauthoring, representability and required local checks. P2–P8 remain unstarted.

Read AGENTS.md, then Docs/ArchitectureNext/00_OVERVIEW.md,
10_ACTIVE_ROADMAP.md, 13_PREFABS_OASISOBJECTS_AND_COMPONENTS.md and
14_PREFAB_AND_POOL_DELIVERY_PLAN.md. For baseline details read relevant sections of
11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md, 12_OASIS_SCRIPT.md and
09_RUNTIME_BUILD_AND_PLAYER_CONTRACT.md; inspect current source before each phase.

Delivered baseline: A1–A7 and A8.1–A8.4; A8.5 initial single-ball Pool script and
integration coverage, merged trigger discoverability and authored development
keyboard bindings. Actual Pool asset wiring and local suite/manual verification
remain outstanding. Preserve single-ball build/load/input/trigger/reset as the P3
gate before multi-ball work. Existing lists/for/range/arithmetic are delivered;
spawning, functions, typed live refs and focused geometry queries are proposed.

Gameplay remains Machine-owned behavior.oasis. Preserve A7 commands/timers and
A8.4 startup/fault/unload semantics; never attach behaviour to Emulation Machines.
Proposed A8.6 fruit-device work is deferred behind this track; A8.7 mole validation
is expanded into P7, with a moving-shelf/coin proving case at P8. Variants are P6,
a separate later milestone, not a Pool prerequisite. No parallel competing plans.

Do not execute builds/tests in Codex. Use static checks and describe local
Windows/.NET and Unity verification. Further application/schema/asset/generated-output
changes require the next explicitly scoped task; this document alone does not
authorise P2 or later implementation.
