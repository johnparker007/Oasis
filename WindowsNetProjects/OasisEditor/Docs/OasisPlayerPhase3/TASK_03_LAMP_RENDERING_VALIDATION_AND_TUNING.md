# Task 03: Lamp Rendering Validation and Tuning

Implemented checkpoint for validating and tuning Player lamp rendering after the dynamic lamp pipeline was proven end-to-end.

## Investigation findings

### Editor Face preview path

- Cabinet Face previews use `FaceDocumentArtworkPreviewRenderer`, which delegates texture-backed lamp previews to `FaceCompositor` and `FaceTexturePreviewRenderer` when runtime render assets are available.
- Static cabinet preview modes support `Live`, `BackgroundOnly`, `LampsOff`, and `LampsAllOn`; `LampsAllOn` creates a temporary runtime state with linked lamp references set to intensity `1.0`.
- Live cabinet preview caches the static Face base, then calls `FaceCompositor.Shared.RenderLampOverlay(...)` so changing lamp intensities only recompose affected pixels.
- The texture preview loads `artwork.png`, `mask.png`, `trayId.png`, `lampIds0.png`, and `lampWeights0.png`; it does not consume `lampIds1` or `lampWeights1`.
- The Editor has no per-lamp colour data and no alternate illuminated artwork layer in the runtime texture preview path. Lamp colour is the authored artwork colour.
- The Editor precomputes an ambient/base image as `artwork.rgb * AmbientStrength`, preserving artwork alpha.
- Runtime lamp intensity is clamped to `0.0..1.0`. Lamp ID `0` and zero weights are skipped. Valid lamp IDs are `1..255`.
- Mask strength is applied as `max(mask.r, mask.g, mask.b) * mask.a * MaskStrength`, quantized to a byte.
- Lamp output is computed as `artwork.rgb * (AmbientStrength + mask * visibleLight * EmissionStrength)` and clamped to 8-bit channel output.
- Default Editor texture-preview controls are `AmbientStrength = 1.0`, `EmissionStrength = 1.15`, `MaskStrength = 1.0`, and `LampIds0ChannelCount = 3`. There is no exposure or HDR/bloom control in this CPU/WPF preview path.
- Skia/WPF preview composition produces SDR 8-bit bitmap output; it is a gamma-space authoring preview rather than an HDR output path.

### Cause of dim Player lamps

The Player shader used the same weak family of model as the Editor preview:

```text
artwork.rgb * (staticBrightness + mask * visibleLampContribution * emissionStrength)
```

with default `emissionStrength = 1.0`, then saturated the result before returning it. This means dark artwork pixels remain dark under moderate multipliers, output cannot exceed `1.0`, and transparent blending cannot create a convincing glow by itself. The runtime data and lookup semantics are working; the dimness is caused primarily by the composition model and SDR clamp, not by lamp numbering, mask lookup, weight decoding, ownership, or lamp-state upload.

## Implementation

The Unity `Oasis/Face` shader now separates base artwork from dynamic lamp emission:

```text
visibleLight = sum(lampBrightness[lampId] * weight)
maskedLamp = mask * saturate(visibleLight)
baseRgb = artwork.rgb * _OasisStaticBrightness
lampColour = lerp(artwork.rgb, white, _OasisLampLift)
lampEmission = lampColour * maskedLamp * _OasisEmissionStrength
finalRgb = baseRgb + lampEmission
finalAlpha = artwork.a
```

Defaults:

- `_OasisStaticBrightness = 1.0`: keeps lamp-off artwork at authored brightness.
- `_OasisMaskStrength = 1.0`: preserves the exported mask semantics.
- `_OasisEmissionStrength = 1.75`: makes assigned lamp regions deliberately brighter without requiring bloom.
- `_OasisLampLift = 0.35`: derives an emission colour from available artwork while lifting dark source pixels toward white so dark masked regions can become visibly luminous.

The shader no longer clamps the final RGB before return, allowing HDR/overbright output where the active URP/camera pipeline supports it. Without HDR/bloom, the additive emission is still stronger than multiplying dark artwork alone. Bloom and post-processing remain optional future polish and were not enabled as part of this task.

## Editor decision

No Editor code was changed in this pass. The Editor preview was useful for confirming texture semantics and relative lamp coverage, but its current 8-bit Skia/WPF renderer is inherently an SDR authoring preview with no HDR, bloom, tonemapping, exposure, per-lamp colour, or illuminated-artwork layer. Matching the new Unity HDR-capable shader exactly would require a broader preview-rendering design change. The current Editor remains acceptable as a subdued preview, while Player now owns the stronger runtime lamp appearance.

## Remaining limitations and follow-up

- Runtime exports still do not include separate per-lamp colours or illuminated artwork textures, so Player lamp colour is deliberately derived from artwork plus `_OasisLampLift`.
- `visibleLight` is bounded before emission so overlapping weighted lamps remain predictable; this avoids runaway output but does not model physical additive bulbs beyond full local intensity.
- Bloom, camera HDR verification, tonemapping, and post-processing authoring controls remain optional future work.
- Emulator lamp integration is not marked complete by this task.

## Manual Unity verification checklist

1. Run a Development Build or the Unity Editor and confirm the automatic lamp diagnostic still starts only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
2. Verify an all-lamp flash makes lamp regions clearly brighter than lamp-off base artwork.
3. Verify the single-lamp sweep lights the expected authored regions and does not illuminate unassigned pixels.
4. Check lamps over dark artwork regions; they should become visibly luminous rather than remaining nearly black.
5. Check lamps over bright artwork regions; detail should remain useful and should not wash out the entire Face.
6. Check overlapping weighted lamps; intensity should rise predictably up to full local lamp amount without unstable runaway output.
7. Set all lamps off and confirm base artwork remains visible at authored brightness.
8. Inspect alpha edges and transparent Face regions; artwork alpha should be preserved.
9. Load a machine with multiple Faces and confirm they share the same machine-owned `256x1` lamp-state texture while retaining independent masks/lookups.
10. Run a non-development build and confirm automatic diagnostics are absent.
