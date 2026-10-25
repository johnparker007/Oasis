# Current Priority for Codex

The active user-authorised workstream is the playable arcade foundation: walking,
safe room placement, explicit machine interaction profiles, basic Pool shots and
a fruit-machine playable loop. Follow Docs/ArchitectureNext/15_PLAYABLE_ARCADE.md.
The first implementation is a single-machine playable preview; multi-machine
composition and the fruit-machine loop follow as separate milestones. The first
implementation passed 150 focused Unity tests, actual-GLB room/shot checks and a
Windows development build/headless startup check; interactive visual checks remain.
P1 authoring is delivered in PR #745; P2 runtime components and the P3 single-ball
checkpoint are merged in PR #746. The Pool-required P4 lifecycle/language/query
subset and P5 calculated Pool setup are merged in PR #747 and locally verified.
Optional values and physics-step kinematic targets remain deferred with P6–P8.
The original PoolTableTest1 project was backed up, explicitly reauthored and
verified in the actual Player with sixteen runtime nodes. The user authorised this sequence.

Read AGENTS.md, then Docs/ArchitectureNext/00_OVERVIEW.md,
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
