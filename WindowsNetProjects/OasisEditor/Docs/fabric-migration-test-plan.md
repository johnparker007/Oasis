# Fabric migration test plan

## Automated managed coverage

ABI tests verify x64 sizes/offsets, inline capacities and Cdecl delegate metadata. Managed fake-session tests should cover request mapping, lifecycle/failure cleanup, elapsed timing and baselines, persistent inputs, immutable snapshot publication/change detection, and audio frame conversion. Wrapper validation covers absolute existing paths, role-local contiguous slots and four-ROM limits.

## Native and hardware coverage

A source-built fake native runtime was not added because the repository's current WPF CI/tooling does not contain a portable Windows native-DLL build step. On Windows, test missing exports, UTF-8 fields, typed resources, configuration bytes, two-call errors, snapshot retry, frame semantics, destruction and module lifetime with a purpose-built fake DLL.

Real `FabricRuntime.dll`, Amber API v2 DLL, proprietary ROM, audible playback, and long-running timing tests were not executed in the Codex Linux environment. Before release, exercise valid and invalid dual-path preferences, start/reset/pause/resume/input/shutdown, lamp logical state versus brightness, signed reels, punctuation attributes, unchanged segment masks, partial audio reads, and repeated startup-failure cleanup.

## Hardening coverage in PR #589

Managed behavioural tests now exercise exact launch identifiers and paths, typed ROM ordering and gap rejection, raw percentage validation, field-level Amber reel/coin mapping, retained fractional elapsed-time conversion, serialized pump/reset access, deterministic pump-failure cleanup, and single shutdown/session/runtime/audio disposal. Factory tests cover enabled selection, invalid enabled configuration, and retain the existing direct-backend and MAME-fallback tests.

The wrapper now transfers ownership only after capability discovery and live-session registration, validates successful snapshot headers/counts/capacities/pointers/UTF-8, reuses geometrically grown native arrays, and validates interleaved audio sample capacity before native reads. A source-built native DLL, real Fabric/Amber DLLs, ROM execution, and audible Windows playback remain Windows-only and were not executed here.
