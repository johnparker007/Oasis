# Cabinet analytic reflection prototype

The face-plane origin is untransformed UV `(0,0)`. Normalized right and up axes lead to UV `(1,0)` and `(0,1)` over the positive width and height; the visible normal is `cross(right, up)`. The shader tests the untransformed rectangle, then uses the shared Face orientation function.

## Manual setup

1. Put `Oasis/CabinetAnalyticReflection` on one cabinet material slot containing a flat area and a modelled step/bevel. The runtime binding deliberately requires this shader and never replaces or mutates a material asset.
2. Create a plane transform at the face UV `(0,0)` corner and construct a validated `RuntimeFaceReflectionPlane` from its world axes. After machine loading, call `RuntimeCabinetReflectionBinding.TryCreate(machine, faceId, renderer, materialIndex, plane, ...)` and retain/dispose the result with the machine view.
3. Move the camera and compare the discontinuity across the step. Switch lamps off, partially on, and fully on; the already-bound shared lamp-state texture updates without rebinding.
4. For rough grey plastic, begin qualitatively with non-metallic, low reflection/unlit-artwork strength, stronger lamp strength, moderate roughness/Fresnel, and modest normal distortion. For polished/chrome, use metallic, higher strengths/smoothness/Fresnel, and low roughness/distortion. Tune for each cabinet rather than treating defaults as production values.

This prototype uses URP PBR for the cabinet base and a fixed five-tap reconstructed-colour softening filter. Each tap decodes point-sampled lamp IDs and weights before colours are averaged. It performs no arbitrary scene or cabinet self-occlusion; use the visibility mask or material separation. Environment reflection fallback, shadow/depth passes, and production manifest integration are follow-up work.
