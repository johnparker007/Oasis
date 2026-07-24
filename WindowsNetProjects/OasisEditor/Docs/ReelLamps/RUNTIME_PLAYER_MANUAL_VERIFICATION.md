# Reel Lamp Runtime Export and Player Verification Checklist

Use this checklist on a Windows development machine with the Oasis Editor toolchain and the Unity Oasis Player available.

## Export package

- Import or open an MFME-derived machine that has top, middle, and bottom reel-lamp assignments.
- Export/build the machine runtime package from Oasis Editor.
- Inspect the generated Face runtime folder and confirm each opaque reel has both `runtime/reels/<reelId>.png` and `runtime/reels/<reelId>_transmissionMask.png`.
- Open `face.runtime.json` and confirm `transmissionMask` values are relative contained paths such as `reels/<reelId>_transmissionMask.png`.
- Confirm `face.runtime.json` contains no authored project paths such as `Assets/...` for reel bands or transmission masks.
- Confirm non-opaque reels omit `transmissionMask` and do not package a redundant transmission-mask PNG.

## Player rendering

- Load the exported runtime package in Oasis Player.
- Drive the top reel-lamp lamp number and confirm only the top field illuminates.
- Drive the middle reel-lamp lamp number and confirm only the middle field illuminates.
- Drive the bottom reel-lamp lamp number and confirm only the bottom field illuminates.
- Drive multiple assigned lamp numbers simultaneously and confirm their contributions add without replacing each other.
- Drive intermediate brightness values and confirm partial brightness is visible rather than snapping to fully on/off.
- Spin the reel and confirm lamp fields remain fixed relative to the cabinet/Face aperture.
- While spinning, confirm symbols and the opaque transmission mask move through the fixed lights.
- For opaque reels, confirm blank/background areas block internal lamp illumination without erasing normally scene-lit artwork.
- For non-opaque reels, confirm the soft circular falloff is visible without transmission-mask clipping.

## Reload and cleanup

- Unload and reload the Face or machine runtime package several times.
- Confirm there are no duplicate generated reel GameObjects.
- Confirm no duplicate reel materials or owned textures remain after unload.
- Confirm reel transmission-mask textures are destroyed/unloaded with their owning Face.
- Confirm lamp-state updates are not duplicated after reload and do not create duplicate subscriptions or bindings.
