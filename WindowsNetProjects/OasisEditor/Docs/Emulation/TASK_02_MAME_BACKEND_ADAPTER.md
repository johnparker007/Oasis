Task 02 - MAME Backend Adapter
Goal

Wrap the existing MAME implementation behind IEmulationBackend.

This task should preserve MAME behaviour.

Scope

Implement:

MameEmulationBackend
Any minimal glue needed to route backend-neutral events into existing runtime adapters
Tests for lifecycle command translation where practical

Do not rewrite:

MameEmulationService
MameProcessRunner
MameStdoutParser
Lua plugin protocol
Existing runtime adapters
Current MAME Path

The current path includes:

MameEmulationService
MameProcessRunner
MameProcessStartInfoBuilder
MameStdoutParser
MameInputCommandService
MAME runtime adapters

The existing lifecycle operations are:

Start
Start and load state
Start debugger
Start debugger and load state
Stop
Save state and exit
Load state
Save state
Pause
Resume
Throttle
Soft reset
Hard reset
MameEmulationBackend

Create a thin adapter implementing IEmulationBackend.

It should delegate to the existing MAME services.

Suggested mapping:

IEmulationBackend.StartAsync
    -> MameEmulationService.StartAsync

IEmulationBackend.StopAsync
    -> MameEmulationService.StopAsync

IEmulationBackend.PauseAsync
    -> MameEmulationService.PauseAsync

IEmulationBackend.ResumeAsync
    -> MameEmulationService.ResumeAsync

IEmulationBackend.ResetAsync(Soft)
    -> MameEmulationService.SoftResetAsync

IEmulationBackend.ResetAsync(Hard)
    -> MameEmulationService.HardResetAsync

IEmulationBackend.SetInputStateAsync
    -> MameInputCommandService.TrySendInputStateAsync
Runtime Events

There are two acceptable implementation paths.

Preferred Incremental Path

Adapt MameStdoutParser so that parsed values can either:

continue to go directly to existing runtime adapters, or
publish backend-neutral events

Do this with minimal churn.

Fallback Path

If changing the parser would be too invasive, leave direct parser-to-runtime-adapter wiring in place for this task and document the follow-up work.

The priority is preserving behaviour.

Capabilities

MAME backend capabilities should likely be:

SupportsPause = true
SupportsResume = true
SupportsSoftReset = true
SupportsHardReset = true
SupportsSaveState = true
SupportsLoadState = true
SupportsThrottle = true
SupportsDebugger = true
Success Criteria
Existing MAME workflow behaves identically.
Existing MAME tests pass.
New tests prove the backend delegates lifecycle calls correctly.
No native DLL code is introduced.
Deliverable Summary

When complete, report:

Files added
Files modified
Behavioural risk
Tests run
Recommended next task
