# Oasis Player Phase 3 Context: Dynamic Face Lamps

Phase 2 is complete: the Player loads cabinet GLB content, loads Face runtime manifests and production textures, resolves `OasisFace_` targets, binds static Face artwork through the dedicated `Oasis/Face` shader, and owns/cleans up Face materials and textures explicitly.

Phase 3 adds dynamic Face lamps without changing the Phase 2 runtime export contract. The production textures remain `artwork.png`, `mask.png`, `trayId.png`, `lampIds0.png`, and `lampWeights0.png`.

## Authoritative runtime texture contract discovered in Editor code

Source of truth:

- `FaceRuntimeTextureGenerator` writes `trayId.png`, `lampIds0.png`, and `lampWeights0.png`.
- `FaceTexturePreviewRenderer` consumes those textures for the Editor texture-driven preview.

Semantics:

- `trayId.png`: RGBA PNG. Red stores the tray ID as an integer byte. Green and blue are zero. Alpha is 255 for tray-owned pixels and 0 for unowned transparent pixels. Tray IDs are one-based in generated bridge trays; ownership prevents later overlapping trays from overwriting earlier tray pixels. The current Player lamp lookup does not bank lamp states by tray; tray is metadata/ownership for the exported pixel region.
- `lampIds0.png`: RGBA PNG. RGB store up to three lamp IDs affecting that pixel. Alpha is 255 for tray-owned pixels and 0 outside authored tray pixels, but alpha is not a fourth lamp contribution in the current production writer. Lamp ID 0 is the invalid/unassigned sentinel. Valid exported lamp IDs are 1 through 255.
- `lampWeights0.png`: RGBA PNG. RGB store the matching contribution weights for the lamp IDs in `lampIds0.png`, quantized as `round(rawWeight * 255)` and decoded as byte/255. Alpha is 255 for tray-owned pixels and 0 outside authored tray pixels; the current production writer preserves RGB data channels plus opaque alpha and supports up to three emitters per tray.
- `mask.png`: RGBA image. The lamp mask value is `max(R,G,B) * alpha * maskStrength`, using normalized channel values. It gates illumination, not artwork opacity.
- Multiple contributions: the current production contract supports up to three contributions per pixel through the RGB channels of `lampIds0.png` and `lampWeights0.png`. Additional `lampIds1`/`lampWeights1` fields exist in manifests but the current export sets them to null and the production writer does not generate them.
- Illumination formula: the Editor texture preview computes `visibleLight = sum(lampBrightness[lampId] * weightByte / 255)`, `light = maskValue * visibleLight * emissionStrength`, then outputs `artworkRgb * (ambientStrength + light)` clamped to 0..255 while preserving artwork alpha.

## Phase boundaries

- Task 01 proves dynamic lamp rendering without emulator integration.
- Task 02 will bridge emulation output into the runtime lamp-state model.
- Task 03 validates and tunes rendering behaviour visually and with more robust render tests.

## Task 03 first-pass lamp rendering tuning result

The first Task 03 pass kept the Phase 3 runtime data contract intact and tuned only the Player Face composition model. It changed Player rendering from a clamped multiplier to additive lamp emission derived from artwork plus `_OasisLampLift`. That made dynamic lamp regions clearly visible, but the shader was still unlit and bright lamps could trend toward white because `_OasisLampLift` deliberately mixed every lamp colour toward white before adding HDR emission.

The Editor texture preview was inspected and continues to provide an SDR authoring preview using `artwork.rgb * (ambientStrength + mask * visibleLight * emissionStrength)`, clamped to 8-bit output, with no per-lamp colours, illuminated artwork layer, exposure, HDR, bloom, or tonemapping controls.

## Task 03 second-pass and PR 561 follow-up Face shader refinement

The Player `Oasis/Face` shader now renders Face output in one premultiplied transparent `UniversalForward` pass. The pass is single-sided (`Cull Back`) and scene-lit by URP spherical-harmonic ambient lighting, the main light, and URP additional per-pixel lights such as point/spot lights. A previous two-pass attempt used a `SRPDefaultUnlit` additive lamp pass, but manual Unity testing showed no visible dynamic lamp output while the base `UniversalForward` pass rendered. The single-pass design guarantees base and lamp calculations execute together without adding renderer features or duplicate draw infrastructure.

The final blend/output equation is:

```text
outputRgb = baseRgb * artwork.a + lampEmission
outputAlpha = artwork.a
Blend One OneMinusSrcAlpha
dst.rgb = outputRgb + dst.rgb * (1 - outputAlpha)
```

Only the base artwork is premultiplied by artwork alpha. Dynamic lamp emission is not attenuated by artwork alpha, scene lighting, light attenuation, or normal direction; it is attenuated by the exported lamp mask, decoded lamp IDs/weights, runtime lamp brightness, and explicit emission controls.

The final base formula is:

```text
baseLighting = SampleSH(normalWS) * _OasisBaseAmbientStrength
             + mainLight.color * saturate(dot(normalWS, mainLight.direction))
               * mainLight.distanceAttenuation * mainLight.shadowAttenuation
               * _OasisBaseMainLightStrength
             + sum(additionalLight.color * saturate(dot(normalWS, additionalLight.direction))
                   * additionalLight.distanceAttenuation * additionalLight.shadowAttenuation
                   * _OasisBaseAdditionalLightStrength)
baseRgb = artwork.rgb * _OasisStaticBrightness * max(baseLighting, 0)
```

The final lamp formula keeps the existing lookup contract and uses bounded artwork chroma rather than luminance-normalized colour directions:

```text
visibleLight = sum(lampBrightness[lampId] * weight)
lampAmount = max(mask.r, mask.g, mask.b) * mask.a * _OasisMaskStrength * saturate(visibleLight)
peak = max(artwork.r, artwork.g, artwork.b)
chroma = artwork.rgb / max(peak, 0.001)
colourConfidence = saturate(peak / _OasisLampMinLuminance)
lampColour = lerp(float3(0.55, 0.50, 0.42), chroma, colourConfidence)
compressedLampAmount = min(1 - exp2(-lampAmount * _OasisLampCompression), _OasisLampMaxLuminance)
lampEmission = lampColour * compressedLampAmount * _OasisEmissionStrength
```

Default runtime controls are centralized in `RuntimeFaceMaterialFactory` and mirrored by shader defaults:

- `_OasisStaticBrightness = 1.0`: authored albedo scale for lamps-off artwork before local lighting.
- `_OasisBaseAmbientStrength = 1.0`: scale for URP spherical-harmonic ambient lighting.
- `_OasisBaseMainLightStrength = 1.0`: scale for the URP main-light diffuse term.
- `_OasisBaseAdditionalLightStrength = 1.0`: scale for URP additional per-pixel lights.
- `_OasisMaskStrength = 1.0`: preserves exported mask semantics.
- `_OasisEmissionStrength = 1.75`: explicit dynamic lamp emission scale.
- `_OasisLampMinLuminance = 0.08`: near-black fallback threshold.
- `_OasisLampMaxLuminance = 2.0`: practical soft-knee cap before emission scaling, still allowing controlled HDR output.
- `_OasisLampCompression = 2.25`: soft-knee response.

Oasis Editor Face orientation controls are target-specific cabinet settings. `CabinetTargetOverride` stores `FrontSide` (`normal`/`inverted`), `FaceRotation` (`0`/`90`/`180`/`270`), and `FaceFlipHorizontal`, and the Editor preview uses those values to reorder quad corners and reverse winding. Runtime build export now writes the normalized values as `frontSide`, `faceRotation`, and `faceFlipHorizontal` on each `machine.runtime.json` Face reference, alongside `faceId`, `assetName`, `cabinetFaceTargetId`, and `manifest`, so the settings remain Face-to-cabinet-target bindings rather than intrinsic `face.runtime.json` artwork asset properties.

Oasis Player loads those fields into `MachineRuntimeFaceReference` and interprets `frontSide` through the `RuntimeFaceFrontSide` enum/helper instead of renderer-side raw string comparisons. `normal` and `inverted` both use the single `Oasis/Face` shader; the runtime-owned material sets `_Cull` to `1`/front with normal sign `-1` for normal targets and `_Cull` to `2`/back with normal sign `1` for inverted targets. The shader remains single-sided through `Cull [_Cull]`, and the normal sign is applied once before ambient/main/additional-light evaluation so the inverted side is treated as the visible lit front. `faceRotation` and `faceFlipHorizontal` are interpreted by `RuntimeFaceTextureOrientation` and written to `_OasisFaceRotationQuarterTurns` and `_OasisFaceFlipHorizontal`; the shader transforms the Face UV once in the vertex stage, and that transformed UV is shared by artwork, mask, tray ID, lamp ID, and lamp weight texture sampling while the lamp-state lookup remains untransformed. No fragment shader front/back branching, double-sided culling, `abs(dot())`, lamp lookup, lamp texture generation, transparency, or artwork export changes are part of this orientation path. Missing required orientation shader properties now fail material creation with a clear warning instead of silently falling back to defaults.

A manual PR validation pass found that `Oasis/FaceInverted` was never selected because Player always received `frontSide` as normal. The confirmed data-loss point was the Editor build command reading the saved Cabinet3D manifest from disk while the Cabinet Editor's current `TargetOverrides` could still be dirty in memory. The build and preview commands now pass the selected Cabinet document's in-memory `CabinetDocument` into `MachineRuntimeBuildService`, while the path is still used for package naming and resolving the GLB. Export also fails clearly when a Face assignment cannot match existing target overrides, making target-ID representation mismatches visible instead of silently exporting default normal. A follow-up manual validation pass confirmed that export/deserialization/material culling worked but Player's semantic mapping was reversed relative to the Editor preview; this indicates Unity's imported GLB Face target winding is opposite to the Editor preview winding, so Player intentionally maps Editor Normal to Unity front-face culling/normal sign `-1` and Editor Inverted to Unity back-face culling/normal sign `1`.

`FaceRotation` and `FaceFlipHorizontal` are now exported and applied by Oasis Player as UV-only orientation settings. They do not alter `_Cull`, `_OasisNormalSign`, cabinet geometry, or which physical side renders.

Colour-space assumptions remain unchanged: artwork is loaded as colour/sRGB-style data; mask, lamp lookup, and lamp-state textures are loaded or created as linear data. Lookup textures remain point-filtered; artwork and mask remain bilinear-filtered; runtime textures remain clamped and mip-free.

Bloom, glow halos, post-processing, and the emulator lamp bridge remain future work. The emulator bridge is not complete.
