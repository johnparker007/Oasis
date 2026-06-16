Task 06 - Epoch Native Backend
Goal

Implement the Epoch native DLL backend after the System6 backend has been validated.

Scope

Use the same native DLL ABI and backend structure created for System6.

Do not begin this task until Task 04 and Task 05 are stable.

Background

The legacy reference class suggests Epoch exports are generally less prefixed than System6 exports.

Examples include:

EPOCHInitialise
LoadROM
Reset
Run
GetAlphaSegments
TurnSwitchOn
TurnSwitchOff
GetLampBright
GetLampOn
GetPosOut
LoadSoundROM
GetStatusLED
EPOCHShutdown

Confirm actual exports from the DLL before binding.

Requirements
Strongly typed delegates
Clear missing-export errors
Backend-neutral runtime events
Input support
Clean shutdown
Success Criteria
Epoch backend starts and stops cleanly.
It loads ROMs.
It publishes lamp/reel/display state.
It accepts inputs.
Existing MAME and System6 paths still work.
