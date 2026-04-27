# MFME Smoke Fixture (Text-Only)

This folder provides a **text-only** MFME extract manifest fixture for manual smoke testing of the WPF import path.

## Files

- `smoke-layout.json`: includes one Background, Lamp, Reel, SevenSegment, and Alpha component.

## Why text-only

Codex web cannot include binary files in generated PRs, so this fixture intentionally does **not** commit any PNG assets.

When running manual smoke tests on a local machine, place optional placeholder files in a sibling extract tree if desired:

- `background/bg-smoke.png`
- `lamps/lamp12.png`
- `reels/reel2-band.png`

The importer should still run without these files and emit missing-asset warnings.
