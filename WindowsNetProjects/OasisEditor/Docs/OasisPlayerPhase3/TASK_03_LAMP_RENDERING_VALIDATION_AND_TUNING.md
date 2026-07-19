# Task 03: Lamp Rendering Validation and Tuning

Implemented checkpoint for validating and tuning Player lamp rendering after the dynamic lamp pipeline was proven end-to-end.

## Second-pass investigation findings

### Current Player shader before this pass

- `Oasis/Face` was a dedicated URP transparent shader with `Blend SrcAlpha OneMinusSrcAlpha`, `Cull Off`, `ZWrite Off`, and `ZTest LEqual`.
- The pass was named `OasisFaceUnlit` and included only URP `Core.hlsl`; no URP lighting functions were used.
- The shader sampled `artwork.png`, `mask.png`, `lampIds0.png`, `lampWeights0.png`, and the machine-owned `256x1` lamp-state texture. It did not use `lampIds1` or `lampWeights1`.
- Lamp IDs were decoded as one-based byte values. ID `0` was ignored; valid IDs `1..255` sampled the lamp-state texture at `(lampId + 0.5) / 256`.
- Weights were decoded as byte-normalized `weight / 255` values. RGB contributions accumulated as `sum(lampBrightness[lampId] * weight)` and were bounded with `saturate` before emission.
- The mask formula was already correct: `max(mask.r, mask.g, mask.b) * mask.a * _OasisMaskStrength`.
- Runtime texture loading already matched the colour-space contract: artwork is created as sRGB/colour data, while mask and lookup textures use Unity's linear texture constructor flag; lamp-state texture is also linear. Lookup and lamp-state textures use point filtering; artwork and mask use bilinear filtering; all runtime textures clamp and have no mipmaps.

### Root causes of the remaining defects

1. **The Face background ignored room lighting.** The base artwork path was `artwork.rgb * _OasisStaticBrightness`, so ambient intensity, main-light colour, main-light intensity, and surface normals had no effect. The cabinet could react to scene light changes while the Face stayed visually flat.
2. **Bright lamps trended toward white.** The first-pass formula used `lampColour = lerp(artwork.rgb, white, _OasisLampLift)`, then added `lampColour * maskedLamp * _OasisEmissionStrength`. The lift helped dark pixels illuminate but also injected neutral white into every active lamp region. Additive HDR output then pushed already-bright channels toward display/tonemap saturation earlier than weaker channels, reducing chroma in saturated red, yellow, blue, and purple artwork.
3. **Transparent alpha was not the primary whitening cause.** The shader preserved `artwork.a`, and the transparent blend mode multiplies the entire source RGB by source alpha. That can soften emission at antialiased edges, but it does not explain interior lamp regions moving toward white. Keeping alpha unchanged avoids opaque fringes and preserves existing transparent-edge behaviour.
4. **Normals and double-sided rendering need a local shader accommodation.** Face targets are rendered with `Cull Off`; depending on exported winding and cabinet orientation, the visible side can have either normal direction relative to the main light. The base-lighting term therefore uses an absolute Lambert factor so the printed artwork reacts to the main light without changing geometry, culling, sorting, or material queue behaviour.
5. **URP version and APIs.** The Player uses Universal Render Pipeline `17.0.4`, so the shader now uses `Lighting.hlsl`, `GetVertexPositionInputs`, `GetVertexNormalInputs`, `SampleSH`, and `GetMainLight()` from the installed URP shader libraries.

## Final Player rendering model

The Unity `Oasis/Face` shader now separates scene-lit printed artwork from dynamic lamp emission:

```text
visibleLight = sum(lampBrightness[lampId] * weight)
maskedLamp = max(mask.r, mask.g, mask.b) * mask.a * _OasisMaskStrength * saturate(visibleLight)

baseLighting = SampleSH(normalWS) * _OasisBaseAmbientStrength
             + mainLight.color * abs(dot(normalWS, mainLight.direction))
               * mainLight.distanceAttenuation * _OasisBaseMainLightStrength
baseRgb = artwork.rgb * _OasisStaticBrightness * max(baseLighting, 0)

artworkLuminance = max(dot(artwork.rgb, float3(0.2126, 0.7152, 0.0722)), 0.001)
hueDirection = artwork.rgb / artworkLuminance
darkBlend = saturate((_OasisLampMinLuminance - artworkLuminance) / _OasisLampMinLuminance)
lampColourDirection = lerp(hueDirection, float3(1.0, 0.82, 0.55), darkBlend)
compressedLamp = 1 - exp2(-maskedLamp * _OasisLampCompression)
lampLuminance = max(dot(artwork.rgb, Rec709Luminance), _OasisLampMinLuminance)
              * min(compressedLamp * _OasisEmissionStrength, _OasisLampMaxLuminance)
lampEmission = lampColourDirection * lampLuminance

finalRgb = baseRgb + lampEmission
finalAlpha = artwork.a
```

### Shader properties and defaults

- `_OasisStaticBrightness = 1.0`: authored albedo scale for lamps-off artwork before local lighting.
- `_OasisBaseAmbientStrength = 1.0`: scale for URP spherical-harmonic ambient lighting on the base artwork.
- `_OasisBaseMainLightStrength = 1.0`: scale for the URP main-light diffuse term on the base artwork.
- `_OasisMaskStrength = 1.0`: preserves the exported Face mask semantics.
- `_OasisEmissionStrength = 1.75`: overall dynamic lamp luminance scale.
- `_OasisLampMinLuminance = 0.18`: minimum source luminance for active lamps, allowing dark ink to glow visibly.
- `_OasisLampMaxLuminance = 2.5`: practical upper bound for lamp luminance before output. The final RGB is still not clamped, so HDR output remains available where the pipeline supports it.
- `_OasisLampCompression = 2.25`: soft-knee response for active lamps. Increasing lamp control produces diminishing luminance growth instead of immediate linear overdrive.

`_OasisLampLift` was removed because its previous meaning was specifically to mix lamp colour toward white, which is the washed-out behaviour this pass fixes.

### Why hue and saturation are preserved

The lamp path no longer multiplies raw artwork by an arbitrarily large scalar and no longer mixes all active lamps toward white. It derives a luminance-normalized colour direction from the artwork, computes lamp brightness as a separate scalar luminance, and then recombines them. Saturated colours therefore keep their chroma ratios longer under HDR/tonemapping. Very dark artwork uses a controlled warm fallback only when the source luminance is below `_OasisLampMinLuminance`, so black or near-black ink can still produce visible illumination without turning every coloured lamp white.

### Transparent rendering considerations

The render state remains transparent and unchanged. Alpha output remains `artwork.a`; lamp calculations do not alter alpha. With `Blend SrcAlpha OneMinusSrcAlpha`, source RGB including HDR emission is still alpha-weighted by Unity during transparent blending. This is intentional for antialiased Face edges and avoids white or opaque fringes. Interior opaque pixels retain the full base-plus-emission result.

## Editor decision

No Oasis Editor code was changed in this pass. The defect is local to the Unity Player shader: the Editor preview remains a non-HDR SDR authoring preview and does not participate in URP scene lighting. Its lamp assignment, coverage, weighting, and relative colour remain useful for authoring validation, while the Player owns the runtime scene-lit diffuse base and HDR-capable emission path. Matching the Player's lighting and tonemapping in WPF/Skia would be a broader preview-rendering feature, not a required schema or shared-calculation fix.

## Preserved runtime behaviour

- No emulator, MAME, or native backend lamp bridge was implemented.
- Runtime manifests, Face manifests, machine manifests, lookup formats, lamp numbering, and lamp texture dimensions were not changed.
- Runtime Face texture ownership, cleanup, material replacement, and shared machine lamp-state texture ownership were not changed.
- The automatic diagnostic remains guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and its sequence remains all-on/all-off/all-on/all-off followed by a `1..255` sweep at `0.1` seconds per lamp.
- Bloom, glow halos, camera HDR settings, tonemapping, post-processing, and project-wide lighting settings remain future work.

## Manual Unity verification checklist

For all visual checks, compare hue preservation, saturation, luminance, detail retention, response to scene lights, visibility with lamps off, and visibility with lamps on.

1. Lamps off, low room-light intensity: Face artwork should dim like printed cabinet artwork but remain readable from ambient light.
2. Lamps off, high room-light intensity: Face artwork should brighten coherently with the cabinet and should not look self-lit.
3. Confirm that the Face base artwork changes when ambient/main-light intensity changes.
4. Confirm that the cabinet and Face react in the same visual direction to room-light edits.
5. All-lamp flash in a dark room: masked assigned lamp regions should remain visible because lamp emission is independent of scene lighting.
6. All-lamp flash in a brightly lit room: lamps should still add visible illumination without turning broad regions white.
7. Individual lamp sweep: each ID from `1` through `255` should affect only pixels assigned to that ID; ID `0` pixels should stay dark/off.
8. Saturated red lamp regions: red hue and saturation should remain identifiable at high brightness.
9. Saturated yellow lamp regions: yellow should brighten without collapsing immediately to flat white.
10. Saturated blue lamp regions: blue should retain useful chroma and not be over-neutralized.
11. Saturated purple lamp regions: both red/blue character and detail should remain visible.
12. Dark artwork regions with active lamps: regions should illuminate visibly via minimum lamp luminance rather than staying black.
13. Bright artwork regions with active lamps: detail should remain useful under the soft-knee response.
14. Pixels influenced by multiple weighted lamps: accumulated weights should increase local brightness predictably up to the bounded full contribution.
15. Transparent and antialiased Face edges: alpha should remain correct with no opaque white fringes.
16. Multiple Faces sharing one machine lamp-state texture: all Faces should react to the shared state while retaining independent masks and lookups.
17. Development Build diagnostic behaviour: automatic all-on/all-off/all-on/all-off and single-lamp sweep should run with no keyboard dependency.
18. Production build with no diagnostic activity: no automatic diagnostic component or lamp sweep should run.

## Remaining limitations and follow-up

- Unity shader compilation and visual tone mapping must still be verified inside the Unity Editor or a Player build because this repository environment does not provide a Unity batchmode compiler.
- Runtime exports still do not include separate per-lamp colours or illuminated artwork textures; the Player therefore derives lamp colour from underlying artwork plus a dark-pixel fallback.
- The shader accounts for ambient spherical harmonics and the URP main light only. Additional per-pixel additional lights, shadows, normal maps, metallic, and glossy PBR behaviour remain intentionally out of scope.
- Bloom/glow halos are not configured by this task and remain optional future polish.
- Emulator lamp integration remains the next Phase 3 bridge task and is not marked complete here.
