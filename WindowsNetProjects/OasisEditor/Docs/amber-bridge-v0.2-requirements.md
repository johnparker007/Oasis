# Amber Bridge missing-operation evidence

This document records integration evidence only; it does not propose ABI signatures.

| Requirement ID | Editor operation | Current direct JPM export | Amber Bridge v0.1.1 limitation | Observed impact | Temporary behaviour | Priority |
|---|---|---|---|---|---|---|
| AB2-INPUT | Set button/switch state | `TurnSwitchOn`, `TurnSwitchOff` | No input API | Interactive controls cannot reach the core | Explicit `NotSupportedException` | High |
| AB2-OUTPUT | Poll lamps, reels and displays | `GetOutputSnapshotSize`, `GetOutputSnapshot` | No output snapshot API | Editor cannot publish visual state | Polling disabled | High |
| AB2-AUDIO | Stream PCM audio | `GetAudioFormat`, `FillAudioFrames` | No audio API | Native System 6 audio is unavailable | Audio startup disabled | Medium |
| AB2-REELS | Configure reel optos | `SetSteps`, `SetOptoStart`, `SetOptoEnd`, `SetOptoInvert` | No reel configuration API | Project reel settings cannot be applied | Optional startup configuration skipped | High |
| AB2-COINS | Configure coin routing | `SetCoinEnable`, `SetCoinValue`, `SetLockoutVal`, `SetLockoutInvert`, `SetEnable`, `SetCounterIn`, `SetCounterOut`, `SetPortIndex`, `SetCoin`, `SetLevel`, `SetFullLevel` | No coin configuration API | Project coin settings cannot be applied | Optional startup configuration skipped | Medium |
| AB2-PERCENT | Configure percentage switch | `SetPercent` | No percentage configuration API | Project percentage setting cannot be applied | Optional startup configuration skipped | Medium |
