# Task 01 Manual Verification Checklist

Use this checklist for local verification because the Codex environment does not include the Windows/WPF and Unity toolchains required to run the full production build and EditMode suites.

## Editor

1. Open or import a machine that has at least one conventional multi-digit 7-segment display source.
2. Confirm the Face preview uses the canonical A-G plus decimal-point geometry and preserves authored bounds, colors, visibility, and machine display reference.
3. Exercise masks for every segment bit: A=0, B=1, C=2, D=3, E=4, F=5, G=6, decimal point=7.
4. Save all assets, close source Panel2D/Face/Cabinet tabs, and build runtime output from the production machine build path.
5. Inspect `face.runtime.json` and confirm `schemaVersion` is `3` and each 7-segment entry declares `topology`, `digitCount`, `onColorHex`, `offColorHex`, and `showDecimalPoint`.

## Oasis Player

1. Load the generated machine package.
2. Confirm the display mounts through the existing Face-to-Cabinet placement path and appears in the authored Face bounds.
3. Confirm digit order, repeated state changes, and decimal-point masks update without mesh rebuilds or unique per-digit materials.
4. Confirm distinct on/off colors, inactive segment visibility, HDR emission/bloom for active segments, reload cleanup, and stable mesh/material counts.

16-segment/alphanumeric display support remains a follow-up task and should not be verified as part of this PR except to confirm it remains unchanged.
