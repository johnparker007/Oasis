# Task 04: Face Renderer Infrastructure — Complete

Task 04 replaces the temporary Task 03 URP Lit/Standard Face material path with permanent Player-side Face renderer infrastructure while preserving the static visual result: base Face artwork appears on the assigned `OasisFace_` cabinet target.

## Architecture

Runtime flow is now:

```text
RuntimeFace
    -> RuntimeFaceRenderBinding
    -> runtime-owned Oasis Face Material
    -> Oasis/Face shader
    -> exported Face textures
    -> resolved cabinet Renderer/material slot
```

`RuntimeFaceRenderer` remains responsible for deterministic target Renderer and material-slot resolution. It keeps the Task 03 safety rule: a Face target must resolve to exactly one non-skinned Renderer and exactly one material slot. Ambiguous targets are skipped with warnings rather than destructively replacing arbitrary materials.

`RuntimeFaceMaterialFactory` now creates one runtime-owned material per Face using the dedicated `Oasis/Face` shader only. It does not silently fall back to `Universal Render Pipeline/Lit` or `Standard`; if `Oasis/Face` is unavailable, the affected Face is left unrendered and a clear non-fatal warning is produced.

## Shader responsibilities

The dedicated Player shader is `Oasis/Face`.

Task 04 shader behaviour is intentionally static:

- sample exported artwork as the visible base colour;
- preserve artwork alpha for transparent cabinet target areas;
- bind the exported mask and lookup textures for the future dynamic lamp path without applying lamp brightness yet;
- use an unlit model so printed/backlit Face artwork remains legible and stable under the Player test-room lighting;
- disable culling because exported target meshes may be viewed from either side during preview;
- use transparent blending (`SrcAlpha`, `OneMinusSrcAlpha`), `ZWrite Off`, `ZTest LEqual`, and the Transparent render queue;
- use deterministic scale/offset of `(1,1)` and `(0,0)` for every bound texture;
- avoid speculative metallic, smoothness, normal-map, or reflection configuration.

The mask follows the existing Editor renderer semantics: the runtime mask is not an opacity cutout. Its effective lamp-mask value is computed from the maximum RGB channel multiplied by alpha and mask strength, then used to gate future lamp illumination. Task 04 binds the mask but does not apply it visibly because no lamps are lit yet.

## Runtime texture contract

Centralized shader property names/property IDs define the Player runtime Face contract:

- `_OasisArtworkTex` — exported `artwork` texture, sampled as colour artwork;
- `_OasisMaskTex` — exported `mask` texture, future lamp illumination mask;
- `_OasisTrayIdTex` — exported `trayId` lookup texture;
- `_OasisLampIds0Tex` — exported `lampIds0` lookup texture;
- `_OasisLampWeights0Tex` — exported `lampWeights0` lookup texture;
- `_OasisStaticBrightness` — static artwork multiplier, fixed to `1` in Task 04;
- `_OasisMaskStrength` — future mask strength, fixed to `1` in Task 04.

Debug textures (`trayId_debug.png`, `lampWeights_debug.png`) remain export diagnostics and are not loaded for production runtime rendering.

## Texture loading and sampling

`RuntimeFaceLoader` continues to require artwork and mask for Face registration, and it now also attempts to load manifest-declared production lookup textures. Missing/invalid lookup textures produce Face-specific warnings but do not block the Face, other Faces, or cabinet loading.

Texture role configuration is centralized:

- artwork uses colour sampling and bilinear filtering;
- mask uses linear runtime sampling and bilinear filtering;
- lookup-data textures use linear sampling and point filtering to preserve encoded IDs/weights;
- all runtime-loaded Face textures clamp wrapping and are created without mipmaps.

All paths remain resolved through the existing build-root containment checks, and filenames are consumed from `face.runtime.json` rather than hard-coded by the Player.

## Ownership and cleanup

Each successful Face render owns exactly one runtime material instance through `RuntimeFaceRenderBinding`. The binding stores the original renderer material array, restores it during disposal, and destroys only the runtime-owned material. `RuntimeFace.UnloadAssets()` disposes the binding first, then unloads all Face-owned runtime texture assets.

This avoids shared-material contamination, supports multiple machine/cabinet instances, and keeps reload cleanup explicit without relying on `Resources.UnloadUnusedAssets`.

## Update hook for later phases

`RuntimeFaceRenderBinding` exposes the smallest useful dynamic-state seam for later lamp work: a dirty/apply boundary (`MarkDynamicStateDirty` / `ApplyDynamicState`) without introducing emulator state, lamp values, buffers, events, or animation in Task 04.

The current ownership model is runtime-owned material per Face. `MaterialPropertyBlock` is deferred until later phases identify frequently changing scalar/vector/buffer state that benefits from it.

## Direct shader versus RenderTexture decision

Task 04 uses direct shader composition on the cabinet Face mesh. The exported contract (`artwork`, `mask`, `trayId`, `lampIds0`, `lampWeights0`) can support future `artwork + mask + lamp lookup data + dynamic lamp state` in the Face shader, so a RenderTexture composition pipeline is not introduced.

## Explicit non-goals

Task 04 does not implement emulator integration, live lamp state, lamp brightness animation, lamp update events, CPU lamp compositing, reels, buttons, segment displays, VFD, dot-matrix displays, input handling, cabinet interaction, Unity scene redesign, new Blender naming conventions, new Editor Face authoring options, mesh/UV replacement, speculative PBR tuning, or production debug UI.

## Acceptance criteria

- Face materials are created with `Oasis/Face` rather than URP Lit/Standard.
- Missing dedicated shader reports a clear non-fatal warning and leaves the affected Face unrendered.
- Static artwork remains bound to the resolved single-renderer/single-slot `OasisFace_` target.
- Mask and production lookup textures are represented in runtime state and bound when present.
- Lookup texture sampling preserves encoded data better than artwork-style colour filtering.
- Runtime material ownership, restoration, and destruction remain explicit and reload-safe.
- One invalid Face does not prevent other Faces from loading or rendering.
- Task 04 adds focused EditMode tests for material selection, binding, isolation, cleanup, warnings, lookup texture material binding, and the future update seam; loader lookup-path coverage remains a recommended local Unity test follow-up because the current EditMode test assembly references only the RuntimeBuild asmdef.
