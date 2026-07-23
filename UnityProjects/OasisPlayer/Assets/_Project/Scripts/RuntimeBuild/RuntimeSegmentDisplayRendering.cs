using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeSegmentDisplayShaderProperties
    {
        public const string ShaderName = "Oasis/SegmentedDisplay";
        public static readonly int SegmentMask = Shader.PropertyToID("_SegmentMask");
        public static readonly int OnColor = Shader.PropertyToID("_OnColor");
        public static readonly int OffColor = Shader.PropertyToID("_OffColor");
        public static readonly int ActiveEmission = Shader.PropertyToID("_ActiveEmission");
        public static readonly int InactiveEmission = Shader.PropertyToID("_InactiveEmission");
        public static readonly int Brightness = Shader.PropertyToID("_Brightness");
    }

    public sealed class RuntimeSegmentDisplayState
    {
        private readonly Dictionary<string, int[]> _masks = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, float[]> _brightness = new Dictionary<string, float[]>(StringComparer.Ordinal);
        public int Revision { get; private set; }
        public bool SetMasks(string machineReference, int[] masks) { if (string.IsNullOrWhiteSpace(machineReference) || masks == null) return false; _masks[machineReference.Trim()] = (int[])masks.Clone(); Revision++; return true; }
        public bool SetBrightness(string machineReference, float[] brightness) { if (string.IsNullOrWhiteSpace(machineReference) || brightness == null) return false; _brightness[machineReference.Trim()] = (float[])brightness.Clone(); Revision++; return true; }
        public int[] GetMasks(string machineReference, int count) { return !string.IsNullOrWhiteSpace(machineReference) && _masks.TryGetValue(machineReference.Trim(), out var m) ? (int[])m.Clone() : new int[Mathf.Max(0, count)]; }
        public float[] GetBrightness(string machineReference, int count) { if (!string.IsNullOrWhiteSpace(machineReference) && _brightness.TryGetValue(machineReference.Trim(), out var b)) return (float[])b.Clone(); var a = new float[Mathf.Max(0, count)]; for (var i = 0; i < a.Length; i++) a[i] = 1f; return a; }
    }

    public sealed class RuntimeSegmentDisplayMeshFactory
    {
        private readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        public int CachedMeshCount { get { return _cache.Count; } }
        public Mesh GetSevenSegmentDigitMesh(bool showDecimalPoint)
        {
            var key = showDecimalPoint ? "7seg.dp.v1" : "7seg.v1";
            if (_cache.TryGetValue(key, out var mesh)) return mesh;
            mesh = BuildSevenSegmentMesh(showDecimalPoint); _cache[key] = mesh; return mesh;
        }
        private static Mesh BuildSevenSegmentMesh(bool showDecimalPoint)
        {
            var shapes = SevenSegmentShapes(showDecimalPoint);
            var vertices = new List<Vector3>(); var uv2 = new List<Vector2>(); var triangles = new List<int>();
            foreach (var s in shapes)
            {
                var start = vertices.Count;
                for (var i = 0; i < s.Points.Length; i++) { vertices.Add(new Vector3(s.Points[i].x - 0.5f, 0.5f - s.Points[i].y, 0f)); uv2.Add(new Vector2(s.Index, 0f)); }
                for (var i = 1; i < s.Points.Length - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
            }
            var mesh = new Mesh { name = keyName(showDecimalPoint) }; mesh.SetVertices(vertices); mesh.SetUVs(1, uv2); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals(); return mesh;
        }
        private static string keyName(bool dp) { return dp ? "Oasis_SevenSegmentDigit_DP" : "Oasis_SevenSegmentDigit"; }
        private struct Shape { public int Index; public Vector2[] Points; public Shape(int i, params Vector2[] p) { Index = i; Points = p; } }
        private static Shape[] SevenSegmentShapes(bool dp)
        {
            var list = new List<Shape> {
                new Shape(0, v(.22f,0), v(.88f,0), v(.93f,.04f), v(.82f,.11f), v(.37f,.11f), v(.29f,.05f)),
                new Shape(1, v(.95f,.07f), v(1,.11f), v(.91f,.48f), v(.78f,.42f), v(.85f,.14f)),
                new Shape(2, v(.74f,.59f), v(.68f,.86f), v(.76f,.93f), v(.82f,.88f), v(.91f,.50f)),
                new Shape(3, v(.08f,.95f), v(.13f,.99f), v(.65f,.99f), v(.71f,.94f), v(.64f,.87f), v(.18f,.87f)),
                new Shape(4, v(.09f,.51f), v(0,.88f), v(.05f,.91f), v(.15f,.84f), v(.23f,.57f)),
                new Shape(5, v(.19f,.11f), v(.09f,.48f), v(.26f,.41f), v(.32f,.11f), v(.26f,.05f)),
                new Shape(6, v(.26f,.45f), v(.15f,.49f), v(.27f,.55f), v(.74f,.55f), v(.84f,.50f), v(.73f,.45f)) };
            if (dp) list.Add(new Shape(7, v(.84f,.88f), v(.98f,.88f), v(.98f,1), v(.84f,1)));
            return list.ToArray();
        }
        private static Vector2 v(float x, float y) { return new Vector2(x, y); }
    }

    public sealed class RuntimeSegmentDisplayRenderer
    {
        private readonly RuntimeSegmentDisplayMeshFactory _meshFactory = new RuntimeSegmentDisplayMeshFactory();
        private readonly List<RuntimeSegmentDigitBinding> _digits = new List<RuntimeSegmentDigitBinding>();
        private Material _material; private int _appliedRevision = -1;
        public int DigitRendererCount { get { return _digits.Count; } }
        public int CachedMeshCount { get { return _meshFactory.CachedMeshCount; } }
        public void RenderDisplays(RuntimeMachine machine) { if (machine == null) throw new ArgumentNullException(nameof(machine)); foreach (var face in machine.Faces) RenderFace(machine, face); }
        public void RenderFace(RuntimeMachine machine, RuntimeFace face)
        {
            if (!RuntimeFacePlacement.TryResolve(face, out var geometry, out var warning)) { machine.AddWarning(warning); return; }
            var entries = face.Manifest.sevenSegmentDisplays ?? Array.Empty<FaceRuntimeSevenSegmentDisplayManifestEntry>();
            foreach (var e in entries) RenderEntry(machine, face, geometry, e);
        }
        private void RenderEntry(RuntimeMachine machine, RuntimeFace face, RuntimeFaceSurfaceGeometry surface, FaceRuntimeSevenSegmentDisplayManifestEntry e)
        {
            if (!RuntimeFacePlacement.ValidateComponent(face, e, out var warning)) { machine.AddWarning(warning); return; }
            if (!string.Equals(e.topology, "sevenSegment", StringComparison.OrdinalIgnoreCase)) { machine.AddWarning($"Unsupported segmented-display topology '{e.topology}' on '{e.objectId}'."); return; }
            var count = Mathf.Max(1, e.digitCount); var root = new GameObject("SegmentDisplay_" + e.objectId); root.transform.SetParent(face.CabinetTarget, false);
            var material = SharedMaterial(machine); var cellW = e.width / count;
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Digit_" + i); go.transform.SetParent(root.transform, false); var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>(); mf.sharedMesh = _meshFactory.GetSevenSegmentDigitMesh(e.showDecimalPoint); mr.sharedMaterial = material;
                var cx = e.x + cellW * (i + .5f); var cy = e.y + e.height * .5f; go.transform.position = surface.FacePointToWorld(cx, cy, face.Manifest.width, face.Manifest.height) + surface.VisibleNormal * RuntimeFacePlacement.DefaultSurfaceClearanceMetres;
                go.transform.rotation = Quaternion.LookRotation(-surface.VisibleNormal, surface.VerticalTangent); go.transform.localScale = new Vector3(surface.PhysicalWidth * cellW / face.Manifest.width, surface.PhysicalHeight * e.height / face.Manifest.height, 1f);
                var b = new RuntimeSegmentDigitBinding(mr, e.machineReference, i, Parse(e.onColorHex, Color.red), Parse(e.offColorHex, new Color(.04f,0,0,1))); _digits.Add(b); b.Apply(0, 1f);
            }
        }
        public bool ApplyDynamicState(RuntimeMachine machine) { if (machine == null || machine.SegmentDisplayState.Revision == _appliedRevision) return false; foreach (var d in _digits) { var m = machine.SegmentDisplayState.GetMasks(d.Reference, d.Index + 1); var b = machine.SegmentDisplayState.GetBrightness(d.Reference, d.Index + 1); d.Apply(d.Index < m.Length ? m[d.Index] : 0, d.Index < b.Length ? b[d.Index] : 1f); } _appliedRevision = machine.SegmentDisplayState.Revision; return true; }
        private Material SharedMaterial(RuntimeMachine machine) { if (_material != null) return _material; var shader = Shader.Find(RuntimeSegmentDisplayShaderProperties.ShaderName); if (shader == null) { machine.AddWarning("Segmented display shader not found: " + RuntimeSegmentDisplayShaderProperties.ShaderName); shader = Shader.Find("Standard"); } _material = new Material(shader) { name = "OasisSegmentedDisplayShared" }; return _material; }
        private static Color Parse(string hex, Color fallback) { if (ColorUtility.TryParseHtmlString(hex, out var c)) return c; return fallback; }
    }

    public sealed class RuntimeSegmentDigitBinding
    {
        private readonly Renderer _renderer; private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock(); private readonly Color _on; private readonly Color _off; public string Reference { get; private set; } public int Index { get; private set; }
        public RuntimeSegmentDigitBinding(Renderer r, string reference, int index, Color on, Color off) { _renderer = r; Reference = reference; Index = index; _on = on; _off = off; }
        public void Apply(int mask, float brightness) { _renderer.GetPropertyBlock(_block); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.SegmentMask, mask); _block.SetColor(RuntimeSegmentDisplayShaderProperties.OnColor, _on); _block.SetColor(RuntimeSegmentDisplayShaderProperties.OffColor, _off); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.ActiveEmission, 2.5f); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.InactiveEmission, .05f); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.Brightness, Mathf.Clamp01(brightness)); _renderer.SetPropertyBlock(_block); }
    }
}
