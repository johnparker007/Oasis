Codex Start Prompt - Emulation Backend Work

You are working in the Oasis repository.

Focus area:

WindowsNetProjects/OasisEditor

First Step

Read these files:

WindowsNetProjects/OasisEditor/Docs/Emulation/EMULATION_BACKEND_CONTEXT.md
WindowsNetProjects/OasisEditor/Docs/Emulation/TASK_01_EMULATION_BACKEND_ABSTRACTIONS.md

Then inspect the current MAME implementation:

WindowsNetProjects/OasisEditor/OasisEditor/MameRuntimeAbstractions.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameEmulationService.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameProcessRunner.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameProcessStartInfoBuilder.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameStdoutParser.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameInputCommandService.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameLampRuntimeAdapter.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameReelRuntimeAdapter.cs
WindowsNetProjects/OasisEditor/OasisEditor/MameSegmentRuntimeAdapter.cs
Task To Start

Begin with:

TASK_01_EMULATION_BACKEND_ABSTRACTIONS.md

Constraints
Preserve existing MAME behaviour.
Do not rewrite MAME internals.
Do not merge native DLL logic into MAME classes.
Do not implement System6 yet.
Do not implement Epoch yet.
Do not create a full snapshot model yet.
Use incremental runtime events initially.
Keep changes small and easy to review.
Keep all existing tests passing.
Prefer names using "Emulation" rather than "Emulator".
Expected Work For Task 01

Create backend-neutral interfaces and models only.

Likely files:

OasisEditor/Emulation/EmulationBackendAbstractions.cs
or equivalent structure matching project conventions.

Add tests only where useful and consistent with the existing test style.

Do not wire these abstractions into the editor yet.

Completion Report

When Task 01 is complete, report:

Files added
Files modified
Tests added
Tests run
Whether existing MAME behaviour was untouched
Any design deviations
Recommended next task
