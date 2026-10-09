# Current Priority for Codex

The active workstream is ArchitectureNext track A (Object3D and Oasis Script).
A1–A7 and A8.1–A8.4 are implemented; merged PR #733 is the A8.4 baseline.
A8.5 delivers the initial single-ball Pool runtime proving case with collection/reset.
Full multi-ball collection and cue-ball handling remain follow-up work.
Actual Pool asset wiring and local toolchain/manual verification remain necessary.

Read AGENTS.md, then Docs/ArchitectureNext/00_OVERVIEW.md,
10_ACTIVE_ROADMAP.md, the Pool/runtime boundary sections of
11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md, 12_OASIS_SCRIPT.md and the relevant
loading/build sections of 09_RUNTIME_BUILD_AND_PLAYER_CONTRACT.md.

Gameplay belongs in Machine-owned behavior.oasis; commands and timers use A7.
Anchors are Machine-space data and live Object3D roots/Rigidbodies are authoritative.
Do not attach scripts to Emulation Machines. Preserve A8.4 fault/unload semantics.
Do not execute builds/tests in Codex; use supported local .NET/Windows and Unity tools.
Fruit-device commands are A8.6; Whac-A-Mole gameplay validation is A8.7.
