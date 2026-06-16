Task 04 - System6 Native Backend
Goal

Implement the first native DLL emulation backend for System6.

This backend should implement IEmulationBackend.

Scope

Create a working System6 backend using the native DLL ABI layer.

Do not implement Epoch yet.

Do not remove or alter the MAME backend.

Responsibilities
Startup

The backend should:

Load the System6 core DLL.
Bind required exports.
Call initialise.
Load ROMs.
Reset the machine.
Start a background run loop.
Run Loop

Create a dedicated background loop.

The loop should:

Run emulation cycles.
Poll output state.
Publish incremental runtime events.
Respect cancellation.
Avoid blocking the WPF UI thread.

Initial target:

30-60Hz polling
Configurable if practical
Cycles

The legacy code calculates System6 cycles per frame using:

8000000 / displayRefreshRate

For the initial backend, use a simple fixed default such as 60Hz unless an existing editor timing service is available.

Avoid relying on monitor refresh rate unless needed.

Runtime Output

Initial output priorities:

Lamps
Reels
Segments / alpha displays
Status LED
Meters
Other outputs

Start with a small stable subset if needed.

Inputs

Map backend-neutral input state to native switch calls.

Likely initial functions:

SYSTEM6TurnSwitchOn
SYSTEM6TurnSwitchOff
Shutdown

Shutdown should:

Stop background loop
Call native shutdown if available
Dispose native library
Transition state correctly
Error Handling

Backend should enter Failed state if startup fails.

Exceptions should include:

DLL path
missing export name if applicable
ROM path information where safe
backend name
Success Criteria
System6 backend starts and stops cleanly.
It can load ROM paths from EmulationLaunchRequest.
It publishes at least lamp and reel updates.
It accepts switch/input changes.
It does not block the UI thread.
Existing MAME tests still pass.
Deliverable Summary

When complete, report:

Files added
Native exports used
Polling strategy
Known gaps
Tests run
Recommended next task
