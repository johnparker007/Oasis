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

## Task 03 second-pass Face shader refinement

The Player `Oasis/Face` shader now separates scene-lit printed artwork from dynamic lamp emission:

```text
visibleLight = sum(lampBrightness[lampId] * weight)
maskedLamp = mask * saturate(visibleLight)
baseLighting = SampleSH(normalWS) * _OasisBaseAmbientStrength
             + mainLight.color * abs(dot(normalWS, mainLight.direction))
               * mainLight.distanceAttenuation * _OasisBaseMainLightStrength
baseRgb = artwork.rgb * _OasisStaticBrightness * max(baseLighting, 0)
artworkLuminance = max(dot(artwork.rgb, Rec709Luminance), 0.001)
hueDirection = artwork.rgb / artworkLuminance
darkBlend = saturate((_OasisLampMinLuminance - artworkLuminance) / _OasisLampMinLuminance)
lampColourDirection = lerp(hueDirection, warmNeutralFallback, darkBlend)
compressedLamp = 1 - exp2(-maskedLamp * _OasisLampCompression)
lampLuminance = max(dot(artwork.rgb, Rec709Luminance), _OasisLampMinLuminance)
              * min(compressedLamp * _OasisEmissionStrength, _OasisLampMaxLuminance)
lampEmission = lampColourDirection * lampLuminance
finalRgb = baseRgb + lampEmission
finalAlpha = artwork.a
```

Default runtime controls are centralized in `RuntimeFaceMaterialFactory` and mirrored by shader defaults:

- `_OasisStaticBrightness = 1.0`: authored albedo scale for lamps-off artwork before local lighting.
- `_OasisBaseAmbientStrength = 1.0`: scale for URP spherical-harmonic ambient lighting.
- `_OasisBaseMainLightStrength = 1.0`: scale for the URP main-light diffuse term.
- `_OasisMaskStrength = 1.0`: preserves exported mask semantics.
- `_OasisEmissionStrength = 1.75`: overall emissive lamp luminance scale.
- `_OasisLampMinLuminance = 0.18`: minimum source luminance used for active lamps so dark artwork can still illuminate.
- `_OasisLampMaxLuminance = 2.5`: practical cap for lamp luminance contribution before output, still allowing HDR values.
- `_OasisLampCompression = 2.25`: soft-knee response; higher lamp controls produce diminishing luminance growth rather than immediate channel saturation.

Colour-space assumptions remain unchanged: artwork is loaded as colour/sRGB-style data; mask, lamp lookup, and lamp-state textures are loaded or created as linear data. Lookup textures remain point-filtered; artwork and mask remain bilinear-filtered; runtime textures remain clamped and mip-free.

The transparent render state remains `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `ZTest LEqual`, and `Cull Off`. The shader preserves `artwork.a` and does not let lamp state alter alpha. RGB, including emissive RGB, is still source-alpha blended by Unity's transparent blending at antialiased edges; this preserves existing transparent-edge behaviour rather than creating opaque glow fringes.

Bloom, glow halos, post-processing, and the emulator lamp bridge remain future work. The emulator bridge is not complete.
