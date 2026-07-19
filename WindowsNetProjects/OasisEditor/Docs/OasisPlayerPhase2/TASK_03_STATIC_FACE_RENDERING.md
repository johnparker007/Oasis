# Task 03: Static Face Rendering

Instantiate static Face surfaces on detected cabinet face targets using exported artwork and mask images.

This task should not implement lamp animation, display rendering, reels, buttons, emulation, or shader-driven behavior.


## Implementation Status

Implemented. Static Face rendering now occurs after Face loading: each loaded RuntimeFace is bound to a safely resolved cabinet renderer/material slot, receives a runtime-owned URP-compatible material instance, and displays its exported artwork texture using deterministic texture scale/offset. The exported mask texture remains loaded for later dynamic renderer work and is not composited in this checkpoint.

## Next Checkpoint

Task 04: Face Renderer Infrastructure. Do not begin Task 04 until requested.
