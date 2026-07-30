# Fabric Amber emulation

OasisEditor supports one runtime emulation architecture:

```text
OasisEditor -> FabricRuntime.dll -> production Amber provider DLL
```

Impact projects use `FabricEmulationBackend`. `None` selects no backend; Epoch and every other platform are explicitly unsupported. The editor validates the Fabric runtime and production provider paths independently and never falls back to another runtime.

OasisEditor loads only `FabricRuntime.dll`. The configured production provider path is passed to Fabric with backend identifier `amber` and machine identifier `jpm-system6`; OasisEditor never loads or invokes the provider directly.

The fixed 1 ms scheduler, nanosecond advancement, bounded catch-up, audio-frame accumulation, snapshot publication, and NAudio buffering remain owned by `FabricEmulationBackend`.
