# Architecture Next Phase 1 — Manual verification

The Linux Codex environment does not contain the Windows/WPF or Unity toolchains. Run this checklist on a Windows development machine after running the Editor and Player test suites.

1. Create a project and confirm `Assets/Machines/<ProjectName>/asset.machine` is created.
2. Open the Machine asset and confirm it is parsed as a Machine document rather than text.
3. Select a reusable Cabinet from the Machine composition.
4. Assign Faces to the detected `OasisFace_*` Cabinet targets from the Machine.
5. Set each logical reel to one of the selected Cabinet's temporary embedded reel specifications.
6. Select a platform and configure its ROM/runtime settings on the Machine.
7. Import an MFME layout and confirm imported input definitions are written to the active Machine.
8. Open the Face and Cabinet independently; confirm neither serialized file contains the Machine's mounted Face or logical-reel assignments.
9. Save, close, and reopen the project and Machine; confirm identity, composition, runtime, and inputs survive.
10. With the saved Machine selected, Build and Preview in Oasis Player.
11. Confirm Cabinet geometry, mounted Faces, physical reels, and target-bound reflections render as before.
12. Create a second Machine with a different platform and ROM configuration; alternate the active selection and confirm settings do not leak.
13. Add a malformed, unreferenced Face asset and confirm building the Machine still succeeds.

Player Phase 1 intentionally parses and retains the emulation runtime definition but does not start Fabric or another emulator backend.
