# Task 01: Runtime Face Export

## Objective

Extend the Editor runtime build so every Face asset referenced by the machine is exported into the generated Oasis Player build.

## Required output

Each referenced Face must be copied into the machine build under a deterministic folder:

```text
Generated/Builds/<MachineName>/
    machine.runtime.json
    cabinet/
        cabinet.runtime.json
        cabinet.glb
    faces/
        <FaceAssetName>/
            face.runtime.json
            artwork.png
            mask.png
            trayId.png
            lampIds0.png
            lampWeights0.png
            trayId_debug.png
            lampWeights_debug.png
```

## Manifest contract

`machine.runtime.json` remains schema/versioned and must list exported faces with:

- `faceId`
- `assetName`
- `cabinetFaceTargetId`
- project-build-relative `manifest` path

Each `face.runtime.json` remains independently versioned and includes dimensions, artwork and mask image filenames, runtime texture filenames, and existing metadata needed for later reconstruction.

## Non-goals

- No Player loading implementation.
- No runtime rendering implementation.
- No Unity material changes.
- No lamp/display/reel/button behavior.
- No emulation integration.
