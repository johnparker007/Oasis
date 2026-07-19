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

The Player `Oasis/Face` shader now renders Face output in two transparent passes. The base pass is single-sided (`Cull Back`) and scene-lit by URP spherical-harmonic ambient lighting, the main light, and URP additional per-pixel lights such as point/spot lights. The emission pass is also single-sided/depth-tested but additive (`Blend One One`, `ColorMask RGB`), so lamp RGB is not multiplied by artwork alpha.

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

Oasis Editor Face orientation controls were investigated. `CabinetTargetOverride` stores `FrontSide` (`normal`/`inverted`), `FaceRotation`, and `FaceFlipHorizontal`, and the Editor preview uses those values to reorder quad corners and reverse winding. Those values are not currently exported in runtime Face references or Face manifests, and Oasis Player only replaces materials on existing `OasisFace_` targets. This pass keeps normal Face geometry correctly single-sided and documents a future backward-compatible manifest extension as the lowest-complexity way to preserve Editor inversion semantics in Player without shader branches.

Colour-space assumptions remain unchanged: artwork is loaded as colour/sRGB-style data; mask, lamp lookup, and lamp-state textures are loaded or created as linear data. Lookup textures remain point-filtered; artwork and mask remain bilinear-filtered; runtime textures remain clamped and mip-free.

Bloom, glow halos, post-processing, and the emulator lamp bridge remain future work. The emulator bridge is not complete.
