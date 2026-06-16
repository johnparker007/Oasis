Task 05 - Editor Backend Integration
Goal

Integrate backend selection into Oasis Editor so Play View can run using either MAME or native DLL backends.

Scope

Update editor orchestration to depend on IEmulationBackend where practical.

Do not remove MAME-specific setup/preferences yet.

Backend Factory

Create:

public interface IEmulationBackendFactory
{
    IEmulationBackend CreateBackend(FruitMachinePlatformType platform);
}

or similar.

The exact shape may include project/settings context if required.

Initial Backend Selection

Use conservative initial mapping.

Suggested first mapping:

MPU4 -> MAME
Impact/System6 -> Native System6 if configured, otherwise MAME if supported
Epoch -> Native Epoch later
Scorpion4 -> MAME
Unknown/None -> no backend

Adjust based on existing FruitMachinePlatformType values.

Editor Commands

Play/start/stop/pause/reset commands should eventually call backend-neutral APIs.

Avoid changing visible UI behaviour unnecessarily.

Runtime Updates

Backend-neutral runtime events should be routed to the same runtime state update path currently used by MAME.

Preferred approach:

Reuse existing lamp/reel/segment runtime adapter logic.
Rename MAME-specific adapter interfaces later only if necessary.
Avoid duplication.
Preferences

Do not fully redesign preferences in this task.

For initial native backend configuration, it is acceptable to use:

explicit DLL path setting
project-local path
temporary configuration field

Document any temporary choice.

Success Criteria
User can choose or infer backend.
MAME still runs as before.
System6 native backend can be launched from the editor if configured.
Runtime lamp/reel state appears in existing panels/faces.
Existing tests pass.
Deliverable Summary

When complete, report:

Files added
Files modified
UI/preferences changes
Migration notes
Tests run
