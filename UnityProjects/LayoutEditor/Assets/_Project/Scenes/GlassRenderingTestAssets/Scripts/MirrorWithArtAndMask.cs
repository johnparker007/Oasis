using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MirrorWithArtAndMask : MonoBehaviour
{
    [Serializable]
    public class Info
    {
        public Camera cam;
        public RTHandle rtMain;
        public RTHandle rtAlt;
    }

    [SerializeField] private Texture2D _artworkTexture;
    [SerializeField] private Texture2D _artworkMirrorMaskTexture;
    [SerializeField] private Texture2D _artworkGlowMaskTexture;
    [SerializeField] private Texture2D _artworkLampMaskTexture;

    public GameObject thisGameObject => mrObj;

    private GameObject mrObj;
    private GameObject camObj;
    private Camera linkedCam;
    private MeshRenderer mr;
    public Material mat;

    [Min(0.001f)] public float renderScale = 1f;
    public Vector2Int renderSize = Vector2Int.zero;

    private float oldRenderScale = -1f;
    private Vector2Int oldRenderSize = new Vector2Int(-1, -1);

    private readonly List<Info> infos = new List<Info>();
    private static readonly Plane[] frustumPlanes = new Plane[6];

    private void Awake()
    {
        mr = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        if (mrObj == null)
            mrObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mrObj.name = name + "_Quad";
        mrObj.transform.SetParent(transform, false);
        mrObj.hideFlags = HideFlags.HideAndDontSave;
        mr = mrObj.GetComponent<MeshRenderer>();

        if (camObj == null)
            camObj = new GameObject(name + "_Camera", typeof(Camera));
        camObj.transform.SetParent(transform, false);
        camObj.hideFlags = HideFlags.HideAndDontSave;
        linkedCam = camObj.GetComponent<Camera>();
        linkedCam.enabled = false;

        if (mat == null)
        {
            mat = new Material(Shader.Find("Oasis/ArtworkAndMaskedMirror"));
        }

        mat.SetTexture("_ArtworkTex", _artworkTexture);
        mat.SetTexture("_MirrorMaskTex", _artworkMirrorMaskTexture);
        mat.SetTexture("_GlowMaskTex", _artworkGlowMaskTexture);
        mat.SetTexture("_LampMaskTex", _artworkLampMaskTexture);

        mr.sharedMaterial = mat;

        RenderPipelineManager.beginCameraRendering += OnCameraRender;
    }

    private void OnValidate()
    {
        if (mat != null)
        {
            mat.SetTexture("_ArtworkTex", _artworkTexture);
            mat.SetTexture("_MirrorMaskTex", _artworkMirrorMaskTexture);
            mat.SetTexture("_GlowMaskTex", _artworkGlowMaskTexture);
            mat.SetTexture("_LampMaskTex", _artworkLampMaskTexture);
        }
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnCameraRender;

        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].rtMain != null) { RTHandles.Release(infos[i].rtMain); infos[i].rtMain = null; }
            if (infos[i].rtAlt != null) { RTHandles.Release(infos[i].rtAlt); infos[i].rtAlt = null; }
        }
        infos.Clear();

        if (mat != null)
        {
            mat.SetTexture("_MainTex", null);
            mat.SetTexture("_AltTex", null);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].rtMain != null) { RTHandles.Release(infos[i].rtMain); infos[i].rtMain = null; }
            if (infos[i].rtAlt != null) { RTHandles.Release(infos[i].rtAlt); infos[i].rtAlt = null; }
        }
        infos.Clear();

        if (mrObj != null) DestroyImmediate(mrObj);
        if (camObj != null) DestroyImmediate(camObj);
    }

    private static bool IsVisible(Camera camera, Bounds bounds)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    private RTHandle AllocMain()
    {
        if (renderSize.x <= 0 || renderSize.y <= 0)
        {
            return RTHandles.Alloc(Vector2.one * renderScale, name: $"{name}_Main");
        }
        return RTHandles.Alloc(renderSize.x, renderSize.y, name: $"{name}_Main");
    }

    private RTHandle AllocAlt()
    {
        if (renderSize.x <= 0 || renderSize.y <= 0)
        {
            return RTHandles.Alloc(Vector2.one * renderScale, name: $"{name}_Alt");
        }

        return RTHandles.Alloc(renderSize.x, renderSize.y, name: $"{name}_Alt");
    }

    private void EnsureReferenceSize()
    {
        if (renderSize.x > 0 && renderSize.y > 0)
        {
            RTHandles.SetReferenceSize(renderSize.x, renderSize.y);
        }
        else
        {
            int w = Mathf.CeilToInt(Screen.width * Mathf.Max(0.001f, renderScale));
            int h = Mathf.CeilToInt(Screen.height * Mathf.Max(0.001f, renderScale));
            RTHandles.SetReferenceSize(w, h);
        }
    }

    private void ReallocAll()
    {
        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].rtMain != null) { RTHandles.Release(infos[i].rtMain); infos[i].rtMain = null; }
            if (infos[i].rtAlt != null) { RTHandles.Release(infos[i].rtAlt); infos[i].rtAlt = null; }
            infos[i].rtMain = AllocMain();
            infos[i].rtAlt = AllocAlt();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;

        if (oldRenderScale != renderScale || oldRenderSize != renderSize)
        {
            // Only adjusts sizes for already-initialized RTHandles.
            EnsureReferenceSize();
            ReallocAll();
            oldRenderScale = renderScale;
            oldRenderSize = renderSize;
        }

        // Do NOT allocate here. Allocation is deferred to beginCameraRendering.
        // We also avoid touching SceneView here entirely.
    }

    private void OnCameraRender(ScriptableRenderContext ctx, Camera cam)
    {
        if (!Application.isPlaying) return;

        // Ignore editor cameras to avoid pre-init allocs
        if (cam.cameraType == CameraType.SceneView || cam.cameraType == CameraType.Preview)
            return;

        if (!IsVisible(cam, mr.bounds))
            return;

        OnWillRenderObjectWCam(cam, true);
    }

    private void OnWillRenderObjectWCam(Camera current, bool renderNow)
    {
        if (!Application.isPlaying) return;
        if (!renderNow) return;                 // never allocate outside render
        if (current == linkedCam) return;

        int camIdx = infos.FindIndex(x => x.cam == current);
        if (camIdx == -1)
        {
            EnsureReferenceSize();              // safe here after URP init
            var info = new Info
            {
                cam = current,
                rtMain = AllocMain(),
                rtAlt = AllocAlt(),
            };
            infos.Add(info);
            camIdx = infos.Count - 1;
        }

        var rt = infos[camIdx].rtMain;
        var rt2 = infos[camIdx].rtAlt;

        if (linkedCam == null || thisGameObject == null) return;

        if (current.stereoEnabled)
        {
            var posR = current.transform.position;
            var rotR = current.transform.rotation;
            var projR = current.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

            var posL = current.transform.position;
            var rotL = current.transform.rotation;
            var projL = current.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);

            RenderCam(current, posR, rotR, projR, rt, "_MainTex", true);
            RenderCam(current, posL, rotL, projL, rt2, "_AltTex", true);
        }
        else
        {
            var pos = current.transform.position;
            var rot = current.transform.rotation;
            var proj = current.projectionMatrix;

            RenderCam(current, pos, rot, proj, rt, "_MainTex", true);
        }
    }

    public void RenderCam(Camera current, Vector3 pos, Quaternion rot, Matrix4x4 proj, RTHandle target, string texId, bool renderNow)
    {
        if (target == null) return;

        // renderNow is always true in this design
        var prevProj = linkedCam.projectionMatrix;
        var prevNear = linkedCam.nearClipPlane;
        var prevPos = linkedCam.transform.position;
        var prevRot = linkedCam.transform.rotation;

        linkedCam.fieldOfView = current.fieldOfView;
        linkedCam.nearClipPlane = current.nearClipPlane;
        linkedCam.farClipPlane = current.farClipPlane;
        linkedCam.projectionMatrix = proj;

        linkedCam.transform.localPosition = Vector3.Reflect(thisGameObject.transform.InverseTransformPoint(pos), Vector3.forward);
        linkedCam.transform.localRotation = Quaternion.LookRotation(
            Vector3.Reflect(thisGameObject.transform.InverseTransformDirection(rot * Vector3.forward), Vector3.forward),
            Vector3.Reflect(thisGameObject.transform.InverseTransformDirection(rot * Vector3.up), Vector3.forward)
        );

        Transform clipPlane = thisGameObject.transform;
        int dot = Math.Sign(Vector3.Dot(clipPlane.forward, (clipPlane.position - linkedCam.transform.position)));

        Vector3 camSpacePos = (linkedCam.worldToCameraMatrix).MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = (linkedCam.worldToCameraMatrix).MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDist = -Vector3.Dot(camSpacePos, camSpaceNormal);
        Vector4 clipPlaneCamSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDist);
        var oblique = linkedCam.CalculateObliqueMatrix(clipPlaneCamSpace);
        linkedCam.projectionMatrix = oblique;

        linkedCam.forceIntoRenderTexture = true;

        var oldrt = linkedCam.targetTexture;
        linkedCam.targetTexture = target;

        RenderPipeline.SubmitRenderRequest(linkedCam, new UniversalRenderPipeline.SingleCameraRequest
        {
            destination = target,
        });

        linkedCam.targetTexture = oldrt;

        linkedCam.projectionMatrix = prevProj;
        linkedCam.nearClipPlane = prevNear;
        linkedCam.transform.position = prevPos;
        linkedCam.transform.rotation = prevRot;

        if (mat != null)
            mat.SetTexture(texId, target);
    }
}
