# Task 03: Lamp Rendering Validation and Tuning

Implemented checkpoint for validating and tuning Player lamp rendering after the dynamic lamp pipeline was proven end-to-end.

## Follow-up investigation findings for PR 561

### Why point lights were ignored

- The first PR 561 shader pass sampled URP spherical-harmonic ambient lighting and `GetMainLight()`, but it did not compile or execute an additional-light loop.
- The Player project uses Universal Render Pipeline `17.0.4`.
- The PC URP asset has additional lights enabled with an additional-lights-per-object limit of `4` and additional-light shadows enabled. The mobile URP asset also enables additional lights with a per-object limit of `4`, but disables additional-light shadows.
- The PC renderer asset is configured for rendering mode `2`, so the custom shader needs the standard URP additional-light variants, including the Forward+ keyword path, rather than relying on a main-light-only custom pass.
- The only other project shader is a legacy surface shader for carpet; there is no existing local URP custom-lighting shader to copy.

### Face winding, culling, and Editor inversion semantics

- Oasis Editor stores Face orientation overrides in `CabinetTargetOverride`: `FrontSide` is either `normal` or `inverted`, `FaceRotation` is `0/90/180/270`, and `FaceFlipHorizontal` is a horizontal Face flip.
- Editor preview geometry applies rotation/flip to the quad corner order, then computes `reverseWinding = FaceFlipHorizontal XOR isInverted` so its WPF preview can render the intended side.
- Runtime machine export currently writes each Face reference with only `FaceId`, `AssetName`, `CabinetFaceTargetId`, and `Manifest`. The Editor's `FrontSide`, `FaceRotation`, and `FaceFlipHorizontal` values are not present in `machine.runtime.json`, `face.runtime.json`, or the Unity `MachineRuntimeFaceReference` model.
- Oasis Player currently resolves an existing `OasisFace_` cabinet target and replaces its material; it does not rebuild Face geometry, flip UVs, reverse winding, or read any inversion metadata.
- Because the inversion data is not exported today, carrying the Editor option through would require a small but real runtime schema/export/Player-model change. This pass does not change runtime schemas. The shader is now correctly single-sided for normal exported Face geometry, and the documented lowest-complexity future approach is to export the three existing target override fields backward-compatibly on `MachineRuntimeFaceReference` and apply them by adjusting target geometry/UVs or culling outside the fragment shader.

### Root causes corrected in this pass

1. **Local point and spot lights were missing.** The shader had no `GetAdditionalLightsCount()`/`GetAdditionalLight(...)` path, so additional URP lights that lit the cabinet could not affect the Face base artwork.
2. **The Face was treated as double-sided.** `Cull Off` plus `abs(dot(normal, lightDirection))` made both sides light as if front-facing. This hid orientation errors and did not match the single-sided printed-artwork model.
3. **Lamp emission was attenuated by artwork alpha.** Returning `baseRgb + lampEmission` through `Blend SrcAlpha OneMinusSrcAlpha` multiplied all source RGB, including emission, by `artwork.a`. This violated the rule that lamps are attenuated only by mask, lamp lookup/state, and explicit lamp controls.
4. **Lamp colour could still become pale/white.** Luminance normalization (`artwork.rgb / artworkLuminance`) can produce colour-direction channels far above `1.0`, and the broad warm fallback blended dark coloured regions toward cream. Additive HDR output and tonemapping then had an easier path to white/pale results.

## Final Player pass design and blend equations

`Oasis/Face` now uses two transparent passes with identical UVs, depth test, and single-sided culling:

### Pass 1: `OasisFaceBaseForwardLit`

- `LightMode = UniversalForward`
- `Blend SrcAlpha OneMinusSrcAlpha`
- `Cull Back`
- `ZWrite Off`
- `ZTest LEqual`
- Output: `half4(baseRgb, artwork.a)`

Blend equation:

```text
dst.rgb = baseRgb * artwork.a + dst.rgb * (1 - artwork.a)
dst.a   = artwork.a + dst.a * (1 - artwork.a)
```

This keeps normal transparent Face artwork and antialiased base edges alpha-controlled.

### Pass 2: `OasisFaceLampEmission`

- `LightMode = SRPDefaultUnlit`
- `Blend One One`
- `ColorMask RGB`
- `Cull Back`
- `ZWrite Off`
- `ZTest LEqual`
- Output: `half4(lampEmission, 0)`

Blend equation:

```text
dst.rgb = lampEmission + dst.rgb
dst.a   = unchanged because ColorMask RGB disables alpha writes
```

This makes lamp emission independent of artwork alpha, scene lighting, light attenuation, normal direction, and ambient lighting. It is still spatially restricted by the rendered single-sided Face geometry, depth-tested against the scene, and numerically restricted by the exported lamp mask, decoded lamp IDs, decoded lamp weights, runtime brightness, and explicit lamp controls.

## Final base-lighting formula

```text
normalWS = NormalizeNormalPerPixel(input.normalWS)
ambient = SampleSH(normalWS) * _OasisBaseAmbientStrength

main = GetMainLight(input.shadowCoord)
mainDiffuse = main.color
            * saturate(dot(normalWS, main.direction))
            * main.distanceAttenuation
            * main.shadowAttenuation
            * _OasisBaseMainLightStrength

additionalDiffuse = 0
for each URP additional per-pixel light visible to the object:
    light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1))
    additionalDiffuse += light.color
                       * saturate(dot(normalWS, light.direction))
                       * light.distanceAttenuation
                       * light.shadowAttenuation
                       * _OasisBaseAdditionalLightStrength

baseLighting = max(ambient + mainDiffuse + additionalDiffuse, 0)
baseRgb = artwork.rgb * _OasisStaticBrightness * baseLighting
```

The base pass compiles the URP variants for main-light shadows, additional lights, additional-light shadows, soft shadows, and Forward+ so point and spot lights can affect the printed Face base through the same additional-light path as supported URP lit objects.

## Final lamp-emission formula

Lamp lookup semantics are unchanged:

```text
visibleLight = sum(DecodeLampBrightness(lampIds0.rgb) * DecodeWeight(lampWeights0.rgb))
lampAmount = max(mask.r, mask.g, mask.b) * mask.a * _OasisMaskStrength * saturate(visibleLight)
```

Colour preservation now uses bounded peak-normalized chroma rather than luminance-normalized overbright direction:

```text
peak = max(artwork.r, artwork.g, artwork.b)
chroma = artwork.rgb / max(peak, 0.001)
colourConfidence = saturate(peak / _OasisLampMinLuminance)
fallback = float3(0.55, 0.50, 0.42)
lampColour = lerp(fallback, chroma, colourConfidence)
```

Lamp intensity remains monotonic and softly compressed:

```text
compressedLampAmount = min(1 - exp2(-lampAmount * _OasisLampCompression), _OasisLampMaxLuminance)
lampEmission = lampColour * compressedLampAmount * _OasisEmissionStrength
```

The dominant artwork colour channel is bounded to `1.0` before explicit emission scaling, so saturated reds, yellows, blues, and purples retain channel ratios better. The fallback activates only when peak RGB is very low, is subdued rather than near-white, and does not override ordinary dark coloured artwork that still has measurable chroma.

## Shader properties and defaults

Defaults are centralized in `RuntimeFaceMaterialFactory` and mirrored in the shader:

- `_OasisStaticBrightness = 1.0`: authored albedo scale for lamps-off artwork before local lighting.
- `_OasisBaseAmbientStrength = 1.0`: scale for URP spherical-harmonic ambient lighting.
- `_OasisBaseMainLightStrength = 1.0`: scale for the URP main-light diffuse term.
- `_OasisBaseAdditionalLightStrength = 1.0`: scale for URP additional per-pixel lights such as point and spot lights.
- `_OasisMaskStrength = 1.0`: preserves exported mask semantics.
- `_OasisEmissionStrength = 1.75`: explicit dynamic lamp emission scale.
- `_OasisLampMinLuminance = 0.08`: low colour-confidence threshold for near-black fallback activation.
- `_OasisLampMaxLuminance = 2.0`: practical soft-knee cap before emission scaling, still allowing controlled HDR output.
- `_OasisLampCompression = 2.25`: soft-knee response; higher lamp controls produce diminishing growth rather than immediate linear overdrive.

`_OasisLampLift` remains removed because mixing active lamp colour toward white was one cause of the washed-out result.

## Editor decision

No Editor rendering code was changed. The Editor preview remains an SDR authoring preview for lamp assignment, coverage, weighting, and relative colour. The existing Editor Face inversion controls were investigated, but their metadata is not exported to the Player runtime manifests. Preserving those inversion semantics in Player should be handled by a future backward-compatible manifest extension and a geometry/UV/culling adjustment outside the fragment shader, not by reintroducing double-sided lighting.

## Preserved runtime behaviour

- No emulator, MAME, or native backend lamp bridge was implemented.
- Runtime lamp numbering, lamp-state texture dimensions, lookup texture formats, lamp-state ownership, update scheduling, cleanup, and material replacement remain unchanged.
- The shader still reads only `lampIds0` and `lampWeights0`; it does not add `lampIds1` or `lampWeights1` support.
- The automatic diagnostic remains guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and its automatic sequence remains all-on/all-off/all-on/all-off followed by a `1..255` sweep at `0.1` seconds per lamp.
- Bloom, glow halos, camera HDR settings, tonemapping, post-processing, and project-wide URP/colour-space settings remain outside scope.

## Manual Unity verification checklist

For all visual checks, compare hue preservation, saturation, luminance, detail retention, response to scene lights, visibility with lamps off, and visibility with lamps on.

1. Lamps off with low ambient light: printed Face base should dim but remain ambient-lit.
2. Lamps off with high ambient light: printed Face base should brighten coherently with the cabinet.
3. Point light off: confirm the Face lacks the local point-light contribution.
4. Point light on near the Face: Face base should brighten like nearby cabinet surfaces.
5. Point light moved closer and farther: distance attenuation should visibly change Face brightness.
6. Point light moved across the Face: local diffuse response should move across the Face consistently with normals.
7. Cabinet and Face comparison under the same point light: both should react in the same direction, allowing for material differences.
8. Main directional light response: rotating or changing intensity should affect the Face base through one-sided Lambert lighting.
9. Face viewed from the back: normal geometry should not render from the back with `Cull Back`.
10. Normal Face quad orientation: intended front side should render and light correctly.
11. Inverted Face quad using the Editor inversion option: currently document as a known Player export gap unless a future manifest extension is implemented.
12. All-lamp flash with dark room lighting: lamps should remain visible because emission is independent of scene lighting.
13. All-lamp flash with bright room lighting: lamps should add visible colour without washing out broad regions.
14. Individual lamp sweep: IDs `1..255` should affect only assigned pixels; ID `0` remains ignored.
15. Saturated red regions: red should stay red at high lamp state.
16. Saturated yellow regions: yellow should brighten without becoming white prematurely.
17. Saturated blue regions: blue should retain useful chroma.
18. Saturated purple regions: purple should retain its red/blue character.
19. Very dark coloured artwork: detectable colour should remain the dominant lamp hue.
20. Near-black artwork: fallback should be restrained and not appear cream/white over normal dark colours.
21. Transparent artwork edges: base alpha edges should remain smooth with no white fringes.
22. Pixels influenced by multiple lamp weights: brightness should accumulate predictably up to the bounded soft-knee response.
23. Multiple Faces sharing one lamp-state texture: all Faces should react to the shared texture with independent masks/lookups.
24. Development Build diagnostic: automatic all-on/all-off/all-on/all-off and sweep should run without keyboard dependency.
25. Production build without diagnostic: automatic diagnostic code should not run.

## Remaining limitations and follow-up

- Unity shader compilation and visual tone mapping must still be verified inside Unity because this repository environment does not include the Unity editor/batchmode compiler.
- Runtime manifests still lack the Editor's Face target orientation overrides. A future small, backward-compatible export should carry `FrontSide`, `FaceRotation`, and `FaceFlipHorizontal` to Player and apply them outside the fragment shader.
- The custom Face shader now covers ambient, main light, and URP additional per-pixel lights; it intentionally does not implement normal maps, metallic/glossy PBR, cookies, or custom glow halos.
- Additional-light shadows depend on the active URP asset/platform support; the PC asset supports them, while the mobile asset currently does not.
- Bloom/glow halos remain optional future polish.
- Emulator lamp integration remains the next Phase 3 bridge task and is not complete.
