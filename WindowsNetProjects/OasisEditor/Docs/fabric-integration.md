# Fabric Amber emulation

OasisEditor supports one runtime emulation architecture:

```text
OasisEditor -> FabricRuntime.dll -> production Amber provider DLL
```

Impact projects use `FabricEmulationBackend`. `None` selects no backend; Epoch and every other platform are explicitly unsupported. The editor validates the Fabric runtime and production provider paths independently and never falls back to another runtime.

OasisEditor loads only `FabricRuntime.dll`. The configured production provider path is passed to Fabric with backend identifier `amber` and machine identifier `jpm-system6`; OasisEditor never loads or invokes the provider directly.

Fabric advancement is now driven by the Oasis Editor runtime update timer. Each eligible editor update calls `IEditorUpdateDrivenEmulationBackend.UpdateAsync`, which measures monotonic elapsed time inside `FabricEmulationBackend`, clamps a single advance to the native display-frame period, processes inputs, publishes a snapshot, and drains available PCM into the existing NAudio sink. The previous independent fixed 1 kHz Fabric pump is intentionally not started in this elapsed-update mode, so there is only one session advancer.

The Fabric/Amber ABI does not currently expose trustworthy machine refresh metadata to managed code. The System 6 fallback is isolated in `FabricEmulationBackend` as 50 Hz (20 ms per native display frame) so it can be replaced with Fabric-reported metadata later. Audio read capacity is calculated with a ceiling from sample rate / native refresh rate (for example, 48 kHz / 50 Hz = 960 frames; 44.1 kHz / 50 Hz = 883 frames) and drained in a bounded loop after each permitted advance.
