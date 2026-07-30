# Fabric-only Amber migration test plan

## Managed coverage

Factory tests cover no-backend selection, Impact selection, unsupported platforms before configuration validation, independent blank/missing DLL errors, and configured audio buffering. Fabric behavior tests retain lifecycle, 1 ms timing, ROM/configuration mapping, input, output snapshots, partial audio reads, failure cleanup, and exact provider/core identifiers.

## Windows native verification

1. Configure valid Fabric runtime and production Amber API v2 provider DLL paths.
2. Start an Impact project and verify lamp, reel, display, input, and audio behavior.
3. Verify missing runtime and provider paths produce distinct actionable errors with no fallback.
4. Verify Epoch and other platforms report unsupported-platform errors even when paths are blank.
5. Inspect loaded modules: OasisEditor loads FabricRuntime.dll; provider loading occurs through Fabric.
6. Confirm no external emulator process is launched.
