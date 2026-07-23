using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeSegmentDisplayShaderProperties
    {
        public const string ShaderName = "Oasis/SegmentedDisplay";
        public static readonly int SegmentMaskLow = Shader.PropertyToID("_SegmentMaskLow");
        public static readonly int SegmentMaskHigh = Shader.PropertyToID("_SegmentMaskHigh");
        public static readonly int OnColor = Shader.PropertyToID("_OnColor");
        public static readonly int OffColor = Shader.PropertyToID("_OffColor");
        public static readonly int ActiveEmission = Shader.PropertyToID("_ActiveEmission");
        public static readonly int InactiveEmission = Shader.PropertyToID("_InactiveEmission");
        public static readonly int Brightness = Shader.PropertyToID("_Brightness");
    }

    public enum SegmentDisplayTopology { SevenSegment, SixteenSegment }

    public static class RuntimeSegmentDisplayTopology
    {
        public const string SevenSegmentName = "sevenSegment";
        public const string SixteenSegmentName = "sixteenSegment";
        public static bool TryParse(string value, out SegmentDisplayTopology topology)
        {
            if (string.Equals(value, SevenSegmentName, StringComparison.OrdinalIgnoreCase) || string.Equals(value, "7seg", StringComparison.OrdinalIgnoreCase)) { topology = SegmentDisplayTopology.SevenSegment; return true; }
            if (string.Equals(value, SixteenSegmentName, StringComparison.OrdinalIgnoreCase) || string.Equals(value, "led16seg", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "led16segsc", StringComparison.OrdinalIgnoreCase)) { topology = SegmentDisplayTopology.SixteenSegment; return true; }
            topology = default; return false;
        }
        public static string ToManifestName(SegmentDisplayTopology topology) { return topology == SegmentDisplayTopology.SevenSegment ? SevenSegmentName : SixteenSegmentName; }
    }

    public sealed class RuntimeSegmentDisplayState
    {
        private readonly Dictionary<string, int[]> _masks = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, float[]> _brightness = new Dictionary<string, float[]>(StringComparer.Ordinal);
        private readonly int[] _emptyMasks = new int[0];
        private readonly float[] _emptyBrightness = new float[0];
        public int Revision { get; private set; }
        public bool SetMasks(string machineReference, int[] masks) { if (string.IsNullOrWhiteSpace(machineReference) || masks == null) return false; _masks[machineReference.Trim()] = (int[])masks.Clone(); Revision++; return true; }
        public bool SetBrightness(string machineReference, float[] brightness) { if (string.IsNullOrWhiteSpace(machineReference) || brightness == null) return false; _brightness[machineReference.Trim()] = (float[])brightness.Clone(); Revision++; return true; }
        public int[] GetMasks(string machineReference) { return !string.IsNullOrWhiteSpace(machineReference) && _masks.TryGetValue(machineReference.Trim(), out var m) ? m : _emptyMasks; }
        public float[] GetBrightness(string machineReference) { return !string.IsNullOrWhiteSpace(machineReference) && _brightness.TryGetValue(machineReference.Trim(), out var b) ? b : _emptyBrightness; }
    }

    public sealed class RuntimeSegmentDisplayMeshFactory
    {
        private const string GeometryVersion = "v2";
        private readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        public int CachedMeshCount { get { return _cache.Count; } }
        public Mesh GetDigitMesh(SegmentDisplayTopology topology, bool showDecimalPoint, bool showCommaTail)
        {
            var key = RuntimeSegmentDisplayTopology.ToManifestName(topology) + ".dp=" + showDecimalPoint + ".comma=" + showCommaTail + "." + GeometryVersion;
            if (_cache.TryGetValue(key, out var mesh)) return mesh;
            mesh = BuildMesh(topology, showDecimalPoint, showCommaTail); mesh.name = "Oasis_" + key; _cache[key] = mesh; return mesh;
        }
        public void Clear() { foreach (var mesh in _cache.Values) if (mesh != null) UnityEngine.Object.Destroy(mesh); _cache.Clear(); }
        private static Mesh BuildMesh(SegmentDisplayTopology topology, bool showDecimalPoint, bool showCommaTail)
        {
            var shapes = topology == SegmentDisplayTopology.SevenSegment ? SevenSegmentShapes(showDecimalPoint) : SixteenSegmentShapes(showDecimalPoint, showCommaTail);
            var vertices = new List<Vector3>(); var uv2 = new List<Vector2>(); var triangles = new List<int>(); var normals = new List<Vector3>();
            foreach (var s in shapes)
            {
                var start = vertices.Count; var localPoints = new Vector3[s.Points.Length];
                for (var i = 0; i < s.Points.Length; i++) { localPoints[i] = new Vector3(s.Points[i].x - 0.5f, 0.5f - s.Points[i].y, 0f); vertices.Add(localPoints[i]); uv2.Add(new Vector2(s.Index, 0f)); normals.Add(Vector3.back); }
                var clockwise = SignedArea(localPoints) < 0f;
                for (var i = 1; i < s.Points.Length - 1; i++) { triangles.Add(start); triangles.Add(start + (clockwise ? i : i + 1)); triangles.Add(start + (clockwise ? i + 1 : i)); }
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(1, uv2); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        private static float SignedArea(Vector3[] points) { var area = 0f; for (var i = 0; i < points.Length; i++) { var next = points[(i + 1) % points.Length]; area += points[i].x * next.y - next.x * points[i].y; } return area * 0.5f; }
        private struct Shape { public int Index; public Vector2[] Points; public Shape(int i, params Vector2[] p) { Index = i; Points = p; } }
        private static Vector2 v(float x, float y) { return new Vector2(x, y); }
        private static Shape[] SevenSegmentShapes(bool dp) { var list = new List<Shape> { new Shape(0, v(.22f,0), v(.88f,0), v(.93f,.04f), v(.82f,.11f), v(.37f,.11f), v(.29f,.05f)), new Shape(1, v(.95f,.07f), v(1,.11f), v(.91f,.48f), v(.78f,.42f), v(.85f,.14f)), new Shape(2, v(.74f,.59f), v(.68f,.86f), v(.76f,.93f), v(.82f,.88f), v(.91f,.50f)), new Shape(3, v(.08f,.95f), v(.13f,.99f), v(.65f,.99f), v(.71f,.94f), v(.64f,.87f), v(.18f,.87f)), new Shape(4, v(.09f,.51f), v(0,.88f), v(.05f,.91f), v(.15f,.84f), v(.23f,.57f)), new Shape(5, v(.19f,.11f), v(.09f,.48f), v(.26f,.41f), v(.32f,.11f), v(.26f,.05f)), new Shape(6, v(.26f,.45f), v(.15f,.49f), v(.27f,.55f), v(.74f,.55f), v(.84f,.50f), v(.73f,.45f)) }; if (dp) list.Add(new Shape(7, v(.84f,.88f), v(.98f,.88f), v(.98f,1), v(.84f,1))); return list.ToArray(); }
        private static Shape[] SixteenSegmentShapes(bool dp, bool comma) { var t=.055f; var list = new List<Shape>(); Action<int,string,float,float,float,float> r=(i,n,x1,y1,x2,y2)=>list.Add(new Shape(i,v(x1,y1),v(x2,y1),v(x2,y2),v(x1,y2))); r(0,"A1",.13f,.02f,.47f,.08f); r(1,"A2",.53f,.02f,.87f,.08f); r(2,"B",.88f,.10f,.95f,.44f); r(3,"C",.88f,.56f,.95f,.90f); r(4,"D2",.53f,.92f,.87f,.98f); r(5,"D1",.13f,.92f,.47f,.98f); r(6,"E",.05f,.56f,.12f,.90f); r(7,"F",.05f,.10f,.12f,.44f); r(8,"G1",.14f,.47f,.47f,.53f); r(9,"G2",.53f,.47f,.86f,.53f); list.Add(new Shape(10,v(.16f,.12f),v(.23f,.12f),v(.47f,.44f),v(.40f,.44f))); list.Add(new Shape(11,v(.53f,.12f),v(.60f,.12f),v(.47f,.44f),v(.40f,.44f))); list.Add(new Shape(12,v(.16f,.88f),v(.23f,.88f),v(.47f,.56f),v(.40f,.56f))); list.Add(new Shape(13,v(.53f,.56f),v(.60f,.56f),v(.84f,.88f),v(.77f,.88f))); r(14,"J",.47f,.10f,.53f,.44f); r(15,"K",.47f,.56f,.53f,.90f); if (dp) list.Add(new Shape(16,v(.86f,.88f),v(.98f,.88f),v(.98f,1),v(.86f,1))); if (comma) list.Add(new Shape(17,v(.74f,.86f),v(.85f,.86f),v(.78f,1),v(.68f,1))); return list.ToArray(); }
    }

    public sealed class RuntimeSegmentDisplayRenderer
    {
        private readonly RuntimeSegmentDisplayMeshFactory _meshFactory = new RuntimeSegmentDisplayMeshFactory(); private readonly List<RuntimeSegmentDigitBinding> _digits = new List<RuntimeSegmentDigitBinding>(); private readonly List<GameObject> _roots = new List<GameObject>(); private Material _material; private int _appliedRevision = -1;
        public int DigitRendererCount { get { return _digits.Count; } } public int CachedMeshCount { get { return _meshFactory.CachedMeshCount; } }
        public void RenderDisplays(RuntimeMachine machine) { if (machine == null) throw new ArgumentNullException(nameof(machine)); Clear(); foreach (var face in machine.Faces) RenderFace(machine, face); }
        public void Clear() { foreach (var root in _roots) if (root != null) UnityEngine.Object.Destroy(root); _roots.Clear(); _digits.Clear(); _meshFactory.Clear(); if (_material != null) UnityEngine.Object.Destroy(_material); _material = null; _appliedRevision = -1; }
        public void RenderFace(RuntimeMachine machine, RuntimeFace face) { if (!RuntimeFacePlacement.TryResolve(face, out var geometry, out var warning)) { machine.AddWarning(warning); return; } foreach (var e in face.Manifest.sevenSegmentDisplays ?? Array.Empty<FaceRuntimeSevenSegmentDisplayManifestEntry>()) RenderEntry(machine, face, geometry, e); foreach (var e in face.Manifest.alphaSegmentDisplays ?? Array.Empty<FaceRuntimeAlphaSegmentDisplayManifestEntry>()) RenderEntry(machine, face, geometry, e); }
        private void RenderEntry(RuntimeMachine machine, RuntimeFace face, RuntimeFaceSurfaceGeometry surface, FaceRuntimeSegmentDisplayManifestEntry e)
        {
            if (!RuntimeFacePlacement.ValidateComponent(face, e, out var warning)) { machine.AddWarning(warning); return; } if (!RuntimeSegmentDisplayTopology.TryParse(e.topology, out var topology)) { machine.AddWarning($"Unsupported segmented-display topology '{e.topology}' on '{e.objectId}'."); return; }
            var count = Mathf.Max(1, e.digitCount); var root = new GameObject("SegmentDisplay_" + e.objectId); root.transform.SetParent(face.CabinetTarget, false); _roots.Add(root); var material = SharedMaterial(machine); var cellW = e.width / count; var mesh = _meshFactory.GetDigitMesh(topology, e.showDecimalPoint, e.showCommaTail);
            for (var i = 0; i < count; i++) { var rendered = e.isReversed ? count - 1 - i : i; var go = new GameObject("Digit_" + rendered); go.transform.SetParent(root.transform, false); var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>(); mf.sharedMesh = mesh; mr.sharedMaterial = material; var cx = e.x + cellW * (rendered + .5f); var cy = e.y + e.height * .5f; go.transform.position = surface.FacePointToWorld(cx, cy, face.Manifest.width, face.Manifest.height) + surface.VisibleNormal * RuntimeFacePlacement.DefaultSurfaceClearanceMetres; go.transform.rotation = Quaternion.LookRotation(-surface.VisibleNormal, surface.VerticalTangent); go.transform.localScale = new Vector3(surface.PhysicalWidth * cellW / face.Manifest.width, surface.PhysicalHeight * e.height / face.Manifest.height, 1f); var b = new RuntimeSegmentDigitBinding(mr, e.machineReference, i, Parse(e.onColorHex, Color.red), Parse(e.offColorHex, new Color(.04f,0,0,1))); _digits.Add(b); b.Apply(0, 1f); }
        }
        public bool ApplyDynamicState(RuntimeMachine machine) { if (machine == null || machine.SegmentDisplayState.Revision == _appliedRevision) return false; foreach (var d in _digits) { var m = machine.SegmentDisplayState.GetMasks(d.Reference); var b = machine.SegmentDisplayState.GetBrightness(d.Reference); d.Apply(d.Index < m.Length ? m[d.Index] : 0, d.Index < b.Length ? b[d.Index] : 1f); } _appliedRevision = machine.SegmentDisplayState.Revision; return true; }
        private Material SharedMaterial(RuntimeMachine machine) { if (_material != null) return _material; var shader = Shader.Find(RuntimeSegmentDisplayShaderProperties.ShaderName); if (shader == null) { machine.AddWarning("Segmented display shader not found: " + RuntimeSegmentDisplayShaderProperties.ShaderName); shader = Shader.Find("Standard"); } _material = new Material(shader) { name = "OasisSegmentedDisplayShared" }; return _material; }
        private static Color Parse(string hex, Color fallback) { if (ColorUtility.TryParseHtmlString(hex, out var c)) return c; return fallback; }
    }

    public sealed class RuntimeSegmentDigitBinding
    {
        private readonly Renderer _renderer; private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock(); private readonly Color _on; private readonly Color _off; public string Reference { get; private set; } public int Index { get; private set; }
        public RuntimeSegmentDigitBinding(Renderer r, string reference, int index, Color on, Color off) { _renderer = r; Reference = reference; Index = index; _on = on; _off = off; }
        public void Apply(int mask, float brightness) { _renderer.GetPropertyBlock(_block); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.SegmentMaskLow, mask & 255); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.SegmentMaskHigh, (mask >> 8) & 1023); _block.SetColor(RuntimeSegmentDisplayShaderProperties.OnColor, _on); _block.SetColor(RuntimeSegmentDisplayShaderProperties.OffColor, _off); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.ActiveEmission, 2.5f); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.InactiveEmission, .05f); _block.SetFloat(RuntimeSegmentDisplayShaderProperties.Brightness, Mathf.Clamp01(brightness)); _renderer.SetPropertyBlock(_block); }
    }
}
