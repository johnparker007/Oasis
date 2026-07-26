# Cabinet analytic reflections

## Architecture and data flow

Analytic reflections add a single Face source to authored reflective cabinet sections without another camera, scene render, CPU lamp reconstruction, or temporal processing.

```text
Face artwork/lookups + shared lamp-state texture
                    |
                    v
        analytic source reconstruction
                    |
camera -> cabinet fragment normal -> reflected ray
                    |
                    v
              Face-plane hit
                    |
                    v
             transformed Face UV
                    |
                    v
      cabinet reflection shader blending
```

`OasisFaceLampCommon.hlsl` supplies the exact Face orientation and lamp decoding used by the Face shader. Every rough-filter tap reconstructs colour before filtering; encoded IDs and weights are never averaged. The same point-filtered, clamp-wrapped 256x1 lamp-state texture is bound once to every section and updates in place.

## Authoring and export

Cabinet reflection definitions are part of `CabinetDocument`. Each has a stable ID, cabinet renderer target name, material slot, source Face ID, explicit cabinet-local Face plane, resolved settings, and optional visibility-mask path relative to the Cabinet asset directory. The runtime exporter packages definitions in cabinet schema version 2 and copies masks into `cabinet/reflection-masks`. Escaping the Cabinet asset directory is rejected.

The selected GLB material slot must be authored with `Oasis/CabinetAnalyticReflection`. This permits several renderers to share one material while independent `MaterialPropertyBlock`s carry source and plane data. Player never mutates or replaces imported material assets.

The plane origin is untransformed Face UV `(0,0)`. Right/up point toward `(1,0)`/`(0,1)` over positive width/height; normal is `cross(right, up)`. Values are cabinet-target local, transformed only after model scale/up-axis correction. Transform vectors preserve non-uniform and mirrored scale; the resulting basis is validated. Bounds are tested before shared rotation/flip. Explicit metadata avoids assumptions about pivots, mesh UV topology, or `transform.right/up`.

After Faces and their runtime materials are created, `RuntimeCabinetReflectionRenderer` resolves exact renderer names and material slots. Invalid definitions add detailed machine warnings without blocking others. `RuntimeMachine` owns bindings and mask textures and restores original property blocks/unloads masks during unload. There is no `Update` path.

## Surface settings

`RoughPlastic` is a starting factory with weak unlit artwork, stronger lamps, low total strength, five reconstructed taps, moderate Fresnel, and modest distortion. `PolishedChrome` starts stronger and sharp, with low distortion. Both resolve to explicit editable values in the document; factory changes cannot alter saved definitions. Ordinary metallic/smooth PBR and URP environment/reflection-probe lighting remain underneath and provide the miss fallback.

Roughness zero reconstructs one source sample. Non-zero roughness uses five fixed taps (centre weighted twice plus four neighbours). Distorted and neighbour UVs are clamped. The visibility mask uses cabinet material UVs, bilinear filtering, and clamp wrapping; absent masks use Unity's white texture. Lamp IDs, weights, and state retain point/clamp settings.

## Graphics, debugging, and lifecycle

Per-definition `enabled` provides graceful disablement while keeping ordinary cabinet PBR. A global graphics toggle and global quality override are deferred until the existing graphics-settings UI has a demonstrated product requirement. Load warnings include definition ID, target ID, slot, Face ID, reason, and available renderer names where useful. Development builds emit one aggregate summary, never per-frame logs.

The shader has Forward, ShadowCaster, DepthOnly, DepthNormals, and Meta passes. Forward supports URP PBR, SH, main/additional lights, Forward+, shadows, fog, metallic/smoothness, normal mapping, and the ordinary environment contribution. Missing/degenerate tangents safely disable normal-map perturbation.

## Performance and limitations

Sharp mode performs one source reconstruction; rough mode performs five. Each reconstruction samples artwork, mask, three ID/state channels, and three weights. Setup allocates two reusable property blocks per binding and one decoded texture per authored mask. It performs no per-frame allocation, rebinding, material creation, extra render, realtime probe update, ray tracing, denoising, or history.

Each section supports one Face. There is no arbitrary scene/cabinet self-occlusion, moving foreground-object reflection, exact reel/display reflection, room-geometry reflection through this source, Render Texture source, or multi-Face shader loop. Deep recesses need masks or material separation. A future Render Texture extension could replace source reconstruction while retaining plane intersection, but is intentionally not implemented.

## Manual validation

Author a stepped grey-plastic section and polished/chrome section with different source Faces/material slots and, optionally, a cabinet-UV mask. Export normally through OasisEditor, load normally in OasisPlayer, move the camera, and switch lamps off/partial/full. Confirm alignment, geometry discontinuities, softness/sharpness, miss environment lighting, shadows/depth, detailed isolation of an invalid target, a machine with zero definitions, and clean unload/reload.
