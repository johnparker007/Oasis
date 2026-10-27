using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia.OpenGL;
using SkiaSharp;

namespace OasisEditor.Avalonia.Rendering3D;

/// <summary>Depth-tested, textured GPU rendering shared by the interactive viewport and MCP captures.</summary>
public sealed unsafe class GlSceneRenderer : IDisposable
{
    private readonly object _gate = new();
    private readonly OffscreenGraphicsContext _context;
    private readonly GlInterface _gl;
    private readonly ReadPixels _readPixels;
    private readonly Uniform3 _uniform3;
    private readonly Uniform4 _uniform4;
    private readonly BlendSeparate _blend;
    private readonly FrontFace _frontFace;
    private readonly GenerateMipmap _mipmap;
    private readonly GetError _getError;
    private readonly ClearDepthFloat? _clearDepthFloat;
    // Live Face materials retain geometry arrays, so output changes do not re-upload vertices.
    private readonly Dictionary<(float[] Vertices, int[] Indices), MeshBuffers> _meshes = [];
    private readonly Dictionary<SceneTexture, int> _textures = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, int> _locations = [];
    private int _program, _framebuffer, _color, _depth, _width, _height;
    private bool _disposed;
    public string Device { get; }
    public GlSceneRenderer()
    {
        _context = new OffscreenGraphicsContext();
        try
        {
            using var current = _context.MakeCurrent();
            _gl = _context.Gl; Device = _context.Description;
            _readPixels = Load<ReadPixels>("glReadPixels");
            _uniform3 = Load<Uniform3>("glUniform3f"); _uniform4 = Load<Uniform4>("glUniform4f");
            _blend = Load<BlendSeparate>("glBlendFuncSeparate"); _frontFace = Load<FrontFace>("glFrontFace");
            _mipmap = Load<GenerateMipmap>("glGenerateMipmap"); _getError = Load<GetError>("glGetError");
            if (_context.IsGles) _clearDepthFloat = Load<ClearDepthFloat>("glClearDepthf");
            InitializeProgram();
        }
        catch { _context.Dispose(); throw; }
    }
    private T Load<T>(string name) where T : Delegate
    {
        var address = _gl.GetProcAddress(name);
        if (address == 0) throw new NotSupportedException($"The graphics driver does not provide {name}.");
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }
    private void InitializeProgram()
    {
        var header = _context.IsGles ? "#version 300 es\nprecision highp float;\n" : "#version 150\n";
        var vertex = _gl.CreateShader(0x8B31); var fragment = _gl.CreateShader(0x8B30);
        try
        {
            var error = _gl.CompileShaderAndGetError(vertex, header + VertexShader);
            if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("3D vertex shader: " + error);
            error = _gl.CompileShaderAndGetError(fragment, header + FragmentShader);
            if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("3D fragment shader: " + error);
            _program = _gl.CreateProgram();
            _gl.AttachShader(_program, vertex); _gl.AttachShader(_program, fragment);
            _gl.BindAttribLocationString(_program, 0, "aPosition");
            _gl.BindAttribLocationString(_program, 1, "aNormal");
            _gl.BindAttribLocationString(_program, 2, "aUv");
            error = _gl.LinkProgramAndGetError(_program);
            if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException("3D shader link: " + error);
        }
        catch { if (_program != 0) _gl.DeleteProgram(_program); _program = 0; throw; }
        finally { _gl.DeleteShader(vertex); _gl.DeleteShader(fragment); }
    }
    public SceneFrame Render(SceneRenderData scene, SceneCameraState camera, int width, int height, Vector4 background,
        bool showVisuals = true, bool showColliders = false, bool showTriggers = false)
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(width));
        camera.Validate();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var current = _context.MakeCurrent();
            EnsureFramebuffer(width, height); Prune(scene);
            _gl.BindFramebuffer(0x8D40, _framebuffer); _gl.Viewport(0, 0, width, height);
            _gl.DepthMask(1); _gl.ClearColor(background.X, background.Y, background.Z, 1);
            // Mesa can expose the desktop glClearDepth symbol in a GLES context even though it is invalid there.
            if (_clearDepthFloat is not null) _clearDepthFloat(1); else _gl.ClearDepth(1);
            _gl.Clear(0x4000 | 0x0100); _gl.Enable(0x0B71); _gl.DepthFunc(0x0203);
            _gl.UseProgram(_program);
            var vp = camera.View * camera.Projection(width / (float)height);
            Matrix("uViewProjection", vp); _uniform3(Location("uEye"), camera.Eye.X, camera.Eye.Y, camera.Eye.Z);
            var visible = scene.Meshes.Where(m => m.Kind switch
            { "collider" => showColliders, "trigger" => showTriggers, "visual" => showVisuals, _ => true }).ToArray();
            var ordered = visible.Where(m => m.Material.AlphaMode != "BLEND")
                .Concat(visible.Where(m => m.Material.AlphaMode == "BLEND").OrderByDescending(m => Vector3.DistanceSquared(camera.Eye, m.Center)));
            var drawCalls = 0; var triangles = 0;
            foreach (var mesh in ordered)
            {
                if (mesh.Indices.Length == 0) continue;
                var buffers = GetBuffers(mesh); var material = mesh.Material;
                Matrix("uModel", mesh.Transform);
                _uniform4(Location("uColor"), material.Color.X, material.Color.Y, material.Color.Z, material.Color.W);
                _uniform3(Location("uEmissive"), material.Emissive.X, material.Emissive.Y, material.Emissive.Z);
                _gl.Uniform1f(Location("uMetallic"), material.Metallic); _gl.Uniform1f(Location("uRoughness"), material.Roughness);
                _gl.Uniform1f(Location("uCutoff"), material.AlphaMode == "MASK" ? material.AlphaCutoff : -1);
                _gl.Uniform1i(Location("uUnlit"), material.Unlit ? 1 : 0);
                Texture(material.BaseTexture, 0, "uBase", "uHasBase");
                Texture(material.EmissiveTexture, 1, "uEmission", "uHasEmission");
                Texture(material.MetallicRoughnessTexture, 2, "uMetalRough", "uHasMetalRough");
                Texture(material.NormalTexture, 3, "uNormalMap", "uHasNormal");
                Texture(material.OcclusionTexture, 4, "uOcclusion", "uHasOcclusion");
                Reflection(mesh.Reflection);
                if (material.DoubleSided || mesh.Lines) _gl.Disable(0x0B44);
                else { _gl.Enable(0x0B44); _frontFace(mesh.Transform.GetDeterminant() < 0 ? 0x0900 : 0x0901); }
                if (material.AlphaMode == "BLEND") { _gl.Enable(0x0BE2); _blend(0x0302, 0x0303, 1, 0x0303); _gl.DepthMask(0); }
                else { _gl.Disable(0x0BE2); _gl.DepthMask(1); }
                _gl.BindVertexArray(buffers.Vao);
                _gl.DrawElements(mesh.Lines ? 0x0001 : 0x0004, mesh.Indices.Length, 0x1405, 0);
                drawCalls++; if (!mesh.Lines) triangles += mesh.Indices.Length / 3;
            }
            _gl.BindVertexArray(0); _gl.Disable(0x0BE2); _gl.DepthMask(1); _gl.Finish();
            var rgba = new byte[width * height * 4];
            fixed (byte* pixels = rgba) _readPixels(0, 0, width, height, 0x1908, 0x1401, (nint)pixels);
            var glError = _getError();
            if (glError != 0) throw new InvalidOperationException($"3D GPU rendering failed (GL 0x{glError:X}).");
            var bgra = new byte[rgba.Length];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var source = ((height - 1 - y) * width + x) * 4; var target = (y * width + x) * 4;
                bgra[target] = rgba[source + 2]; bgra[target + 1] = rgba[source + 1]; bgra[target + 2] = rgba[source]; bgra[target + 3] = 255;
            }
            return new(width, height, bgra, Device, drawCalls, triangles);
        }
    }
    private void EnsureFramebuffer(int width, int height)
    {
        if (_width == width && _height == height) return;
        ReleaseFramebuffer();
        _framebuffer = _gl.GenFramebuffer(); _gl.BindFramebuffer(0x8D40, _framebuffer);
        _color = _gl.GenTexture(); _gl.BindTexture(0x0DE1, _color);
        _gl.TexImage2D(0x0DE1, 0, 0x8058, width, height, 0, 0x1908, 0x1401, 0);
        _gl.TexParameteri(0x0DE1, 0x2801, 0x2601); _gl.TexParameteri(0x0DE1, 0x2800, 0x2601);
        _gl.FramebufferTexture2D(0x8D40, 0x8CE0, 0x0DE1, _color, 0);
        _depth = _gl.GenRenderbuffer(); _gl.BindRenderbuffer(0x8D41, _depth);
        _gl.RenderbufferStorage(0x8D41, 0x81A6, width, height);
        _gl.FramebufferRenderbuffer(0x8D40, 0x8D00, 0x8D41, _depth);
        if (_gl.CheckFramebufferStatus(0x8D40) != 0x8CD5) { ReleaseFramebuffer(); throw new InvalidOperationException("Could not create the 3D color/depth framebuffer."); }
        _width = width; _height = height;
    }
    private MeshBuffers GetBuffers(SceneRenderMesh mesh)
    {
        var key = (mesh.Vertices,mesh.Indices);
        if (_meshes.TryGetValue(key, out var value)) return value;
        if (mesh.Vertices.Length % 8 != 0 || mesh.Vertices.Any(v => !float.IsFinite(v))
            || mesh.Indices.Any(i => i < 0 || i >= mesh.Vertices.Length / 8)
            || mesh.Indices.Length % (mesh.Lines ? 2 : 3) != 0) throw new InvalidDataException($"Invalid geometry for {mesh.Name}.");
        var vao = _gl.GenVertexArray(); var vertex = _gl.GenBuffer(); var index = _gl.GenBuffer();
        try
        {
            _gl.BindVertexArray(vao); _gl.BindBuffer(0x8892, vertex);
            fixed (float* p = mesh.Vertices) _gl.BufferData(0x8892, mesh.Vertices.Length * sizeof(float), (nint)p, 0x88E4);
            _gl.BindBuffer(0x8893, index);
            fixed (int* p = mesh.Indices) _gl.BufferData(0x8893, mesh.Indices.Length * sizeof(int), (nint)p, 0x88E4);
            _gl.VertexAttribPointer(0, 3, 0x1406, 0, 32, 0); _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 3, 0x1406, 0, 32, 12); _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(2, 2, 0x1406, 0, 32, 24); _gl.EnableVertexAttribArray(2);
            value = new(vao, vertex, index); _meshes.Add(key, value); return value;
        }
        catch { _gl.DeleteVertexArray(vao); _gl.DeleteBuffer(vertex); _gl.DeleteBuffer(index); throw; }
    }
    private void Texture(SceneTexture? texture, int unit, string sampler, string enabled)
    {
        _gl.Uniform1i(Location(enabled), texture is null ? 0 : 1);
        _gl.Uniform1i(Location(sampler), unit); _gl.ActiveTexture(0x84C0 + unit);
        if (texture is null) { _gl.BindTexture(0x0DE1, 0); return; }
        if (!_textures.TryGetValue(texture, out var handle))
        {
            using var image = texture.RgbaPixels is null ? SKImage.FromEncodedData(texture.EncodedImage) ?? throw new InvalidDataException("A 3D material texture could not be decoded.") : null;
            var width = image?.Width ?? texture.Width; var height = image?.Height ?? texture.Height;
            if (width is < 1 or > 8192 || height is < 1 or > 8192) throw new InvalidDataException("A 3D material texture has invalid dimensions.");
            var rgba = texture.RgbaPixels ?? new byte[width * height * 4];
            if (rgba.Length != width * height * 4) throw new InvalidDataException("A 3D material texture has invalid pixel storage.");
            if (image is not null) fixed (byte* p = rgba)
                if (!image.ReadPixels(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), (nint)p, width * 4, 0, 0))
                    throw new InvalidDataException("A 3D material texture could not be read.");
            handle = _gl.GenTexture(); _gl.BindTexture(0x0DE1, handle);
            fixed (byte* p = rgba) _gl.TexImage2D(0x0DE1, 0, 0x8058, width, height, 0, 0x1908, 0x1401, (nint)p);
            _gl.TexParameteri(0x0DE1, 0x2802, texture.WrapS is 33071 or 33648 ? texture.WrapS : 10497);
            _gl.TexParameteri(0x0DE1, 0x2803, texture.WrapT is 33071 or 33648 ? texture.WrapT : 10497);
            _gl.TexParameteri(0x0DE1, 0x2800, texture.MagFilter == 9728 ? 9728 : 9729);
            _gl.TexParameteri(0x0DE1, 0x2801, texture.MinFilter is 9728 or 9729 or >= 9984 and <= 9987 ? texture.MinFilter : 9729);
            _mipmap(0x0DE1); _textures.Add(texture, handle);
        }
        else _gl.BindTexture(0x0DE1, handle);
    }
    private void Reflection(SceneReflectionBinding? reflection)
    {
        var count = reflection?.Enabled == true ? Math.Min(reflection.Sources.Count, 4) : 0;
        _gl.Uniform1i(Location("uReflectionEnabled"), count > 0 ? 1 : 0);
        _gl.Uniform1i(Location("uReflectionCount"), count);
        if (reflection is null) return;
        _gl.Uniform1f(Location("uReflectionStrength"), reflection.Strength);
        _gl.Uniform1f(Location("uReflectionFresnelPower"), reflection.FresnelPower);
        _gl.Uniform1f(Location("uReflectionFresnelStrength"), reflection.FresnelStrength);
        _gl.Uniform1f(Location("uReflectionUnlitArtworkStrength"), reflection.UnlitArtworkStrength);
        _gl.Uniform1f(Location("uReflectionLitLampStrength"), reflection.LitLampStrength);
        _gl.Uniform1f(Location("uReflectionRoughness"), reflection.Roughness);
        _gl.Uniform1f(Location("uReflectionDistortion"), reflection.Distortion);
        _gl.Uniform1f(Location("uReflectionEdgeFade"), reflection.EdgeFade);
        for (var i = 0; i < 4; i++)
        {
            var source = i < count ? reflection.Sources[i] : null;
            Texture(source?.Texture, 5 + i, $"uReflectionBase{i}", $"uHasReflectionBase{i}");
            Texture(source?.LampOverlayTexture, 9 + i, $"uReflectionLamp{i}", $"uHasReflectionLamp{i}");
            if (source is null) continue;
            _uniform3(Location($"uReflectionOrigin{i}"), source.Origin.X, source.Origin.Y, source.Origin.Z);
            _uniform3(Location($"uReflectionRight{i}"), source.Right.X, source.Right.Y, source.Right.Z);
            _uniform3(Location($"uReflectionUp{i}"), source.Up.X, source.Up.Y, source.Up.Z);
            _uniform3(Location($"uReflectionNormal{i}"), source.VisibleNormal.X, source.VisibleNormal.Y, source.VisibleNormal.Z);
            _uniform4(Location($"uReflectionSizeUv{i}"), source.Width, source.Height, source.UvQuarterTurns, source.FlipHorizontal ? 1 : 0);
        }
        Texture(reflection.VisibilityMask, 13, "uReflectionVisibility", "uHasReflectionVisibility");
    }
    private void Prune(SceneRenderData scene)
    {
        var usedMeshes = scene.Meshes.Select(mesh => (mesh.Vertices,mesh.Indices)).ToHashSet();
        foreach (var item in _meshes.Keys.Where(m => !usedMeshes.Contains(m)).ToArray())
        { var b = _meshes[item]; _gl.DeleteVertexArray(b.Vao); _gl.DeleteBuffer(b.Vertex); _gl.DeleteBuffer(b.Index); _meshes.Remove(item); }
        var usedTextures = new HashSet<SceneTexture>(ReferenceEqualityComparer.Instance);
        foreach (var m in scene.Meshes)
        {
            var material = m.Material;
            foreach (var t in new[] { material.BaseTexture, material.EmissiveTexture, material.MetallicRoughnessTexture, material.NormalTexture, material.OcclusionTexture }) if (t is not null) usedTextures.Add(t);
            if (m.Reflection is not { } reflection) continue;
            if (reflection.VisibilityMask is not null) usedTextures.Add(reflection.VisibilityMask);
            foreach (var source in reflection.Sources) { usedTextures.Add(source.Texture); if (source.LampOverlayTexture is not null) usedTextures.Add(source.LampOverlayTexture); }
        }
        foreach (var t in _textures.Keys.Where(t => !usedTextures.Contains(t)).ToArray()) { _gl.DeleteTexture(_textures[t]); _textures.Remove(t); }
    }
    private int Location(string name)
    { if (!_locations.TryGetValue(name, out var value)) _locations[name] = value = _gl.GetUniformLocationString(_program, name); return value; }
    private void Matrix(string name, Matrix4x4 matrix) => _gl.UniformMatrix4fv(Location(name), 1, false, &matrix);
    private void ReleaseFramebuffer()
    {
        if (_depth != 0) _gl.DeleteRenderbuffer(_depth);
        if (_color != 0) _gl.DeleteTexture(_color);
        if (_framebuffer != 0) _gl.DeleteFramebuffer(_framebuffer);
        _depth = _color = _framebuffer = _width = _height = 0;
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            try
            {
                using var current = _context.MakeCurrent();
                Prune(SceneRenderData.Empty); ReleaseFramebuffer(); if (_program != 0) _gl.DeleteProgram(_program); _program = 0;
            }
            finally { _context.Dispose(); _disposed = true; }
        }
    }
    private sealed record MeshBuffers(int Vao, int Vertex, int Index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReadPixels(int x, int y, int width, int height, int format, int type, nint data);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Uniform3(int location, float x, float y, float z);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Uniform4(int location, float x, float y, float z, float w);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void BlendSeparate(int rgbSource, int rgbDestination, int alphaSource, int alphaDestination);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FrontFace(int mode);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GenerateMipmap(int target);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetError();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ClearDepthFloat(float depth);
    private const string VertexShader = """
        in vec3 aPosition; in vec3 aNormal; in vec2 aUv;
        uniform mat4 uModel; uniform mat4 uViewProjection;
        out vec3 vPosition; out vec3 vNormal; out vec2 vUv;
        void main() { vec4 p=uModel*vec4(aPosition,1.0); vPosition=p.xyz;
          vNormal=transpose(inverse(mat3(uModel)))*aNormal; vUv=aUv; gl_Position=uViewProjection*p; }
        """;
    private const string FragmentShader = """
        in vec3 vPosition; in vec3 vNormal; in vec2 vUv; out vec4 fragment;
        uniform vec4 uColor; uniform vec3 uEmissive; uniform vec3 uEye;
        uniform float uMetallic; uniform float uRoughness; uniform float uCutoff;
        uniform int uUnlit; uniform int uHasBase; uniform int uHasEmission; uniform int uHasMetalRough; uniform int uHasNormal; uniform int uHasOcclusion;
        uniform sampler2D uBase; uniform sampler2D uEmission; uniform sampler2D uMetalRough; uniform sampler2D uNormalMap; uniform sampler2D uOcclusion;
        uniform int uReflectionEnabled; uniform int uReflectionCount;
        uniform float uReflectionStrength; uniform float uReflectionFresnelPower; uniform float uReflectionFresnelStrength;
        uniform float uReflectionRoughness; uniform float uReflectionDistortion; uniform float uReflectionEdgeFade;
        uniform vec3 uReflectionOrigin0; uniform vec3 uReflectionOrigin1; uniform vec3 uReflectionOrigin2; uniform vec3 uReflectionOrigin3;
        uniform vec3 uReflectionRight0; uniform vec3 uReflectionRight1; uniform vec3 uReflectionRight2; uniform vec3 uReflectionRight3;
        uniform vec3 uReflectionUp0; uniform vec3 uReflectionUp1; uniform vec3 uReflectionUp2; uniform vec3 uReflectionUp3;
        uniform vec3 uReflectionNormal0; uniform vec3 uReflectionNormal1; uniform vec3 uReflectionNormal2; uniform vec3 uReflectionNormal3;
        uniform vec4 uReflectionSizeUv0; uniform vec4 uReflectionSizeUv1; uniform vec4 uReflectionSizeUv2; uniform vec4 uReflectionSizeUv3;
        uniform int uHasReflectionBase0; uniform int uHasReflectionBase1; uniform int uHasReflectionBase2; uniform int uHasReflectionBase3;
        uniform int uHasReflectionLamp0; uniform int uHasReflectionLamp1; uniform int uHasReflectionLamp2; uniform int uHasReflectionLamp3;
        uniform int uHasReflectionVisibility;
        uniform sampler2D uReflectionBase0; uniform sampler2D uReflectionBase1; uniform sampler2D uReflectionBase2; uniform sampler2D uReflectionBase3;
        uniform sampler2D uReflectionLamp0; uniform sampler2D uReflectionLamp1; uniform sampler2D uReflectionLamp2; uniform sampler2D uReflectionLamp3;
        uniform sampler2D uReflectionVisibility;
        uniform float uReflectionUnlitArtworkStrength; uniform float uReflectionLitLampStrength;
        vec3 PlaneOrigin(int i){if(i==0)return uReflectionOrigin0;if(i==1)return uReflectionOrigin1;if(i==2)return uReflectionOrigin2;return uReflectionOrigin3;}
        vec3 PlaneRight(int i){if(i==0)return uReflectionRight0;if(i==1)return uReflectionRight1;if(i==2)return uReflectionRight2;return uReflectionRight3;}
        vec3 PlaneUp(int i){if(i==0)return uReflectionUp0;if(i==1)return uReflectionUp1;if(i==2)return uReflectionUp2;return uReflectionUp3;}
        vec3 PlaneNormal(int i){if(i==0)return uReflectionNormal0;if(i==1)return uReflectionNormal1;if(i==2)return uReflectionNormal2;return uReflectionNormal3;}
        vec4 PlaneSizeUv(int i){if(i==0)return uReflectionSizeUv0;if(i==1)return uReflectionSizeUv1;if(i==2)return uReflectionSizeUv2;return uReflectionSizeUv3;}
        vec2 SourceUv(vec2 uv,int i){vec4 orient=PlaneSizeUv(i);uv=vec2(1.0)-uv;int turn=int(orient.z+0.5);if(turn==1)uv=vec2(1.0-uv.y,uv.x);else if(turn==2)uv=vec2(1.0)-uv;else if(turn==3)uv=vec2(uv.y,1.0-uv.x);if(orient.w>0.5)uv.x=1.0-uv.x;return clamp(uv,vec2(0.0),vec2(1.0));}
        vec3 SampleFace(int i,vec2 uv){vec2 q=SourceUv(uv,i);vec4 base=vec4(0.0);vec4 lamp=vec4(0.0);
          if(i==0){if(uHasReflectionBase0!=0)base=texture(uReflectionBase0,q);if(uHasReflectionLamp0!=0)lamp=texture(uReflectionLamp0,q);}
          else if(i==1){if(uHasReflectionBase1!=0)base=texture(uReflectionBase1,q);if(uHasReflectionLamp1!=0)lamp=texture(uReflectionLamp1,q);}
          else if(i==2){if(uHasReflectionBase2!=0)base=texture(uReflectionBase2,q);if(uHasReflectionLamp2!=0)lamp=texture(uReflectionLamp2,q);}
          else {if(uHasReflectionBase3!=0)base=texture(uReflectionBase3,q);if(uHasReflectionLamp3!=0)lamp=texture(uReflectionLamp3,q);}
          return pow(max(base.rgb,vec3(0.0)),vec3(2.2))*uReflectionUnlitArtworkStrength+pow(max(lamp.rgb,vec3(0.0)),vec3(2.2))*lamp.a*uReflectionLitLampStrength;}
        vec3 ApplyReflection(vec3 color,vec3 N,vec3 normalTS){
          if(uReflectionEnabled==0||uReflectionCount==0)return color;
          vec3 incident=normalize(vPosition-uEye),ray=reflect(incident,normalize(N));float nearest=1e30;int selected=-1;vec2 planeUv=vec2(0.0);
          for(int i=0;i<4;i++){if(i>=uReflectionCount)break;vec3 n=PlaneNormal(i);float denominator=dot(ray,n);if(denominator>=-0.00001)continue;
            float t=dot(PlaneOrigin(i)-vPosition,n)/denominator;if(t<=0.00001||t>=nearest)continue;vec3 hit=vPosition+ray*t;vec4 s=PlaneSizeUv(i);
            vec2 uv=vec2(dot(hit-PlaneOrigin(i),PlaneRight(i))/s.x,dot(hit-PlaneOrigin(i),PlaneUp(i))/s.y);
            if(any(lessThan(uv,vec2(0.0)))||any(greaterThan(uv,vec2(1.0))))continue;nearest=t;selected=i;planeUv=uv;}
          if(selected<0)return color;vec2 uv=clamp(planeUv+normalTS.xy*uReflectionDistortion,vec2(0.0),vec2(1.0));
          vec3 reflectedColor=SampleFace(selected,uv);if(uReflectionRoughness>0.001){vec2 d=vec2(uReflectionRoughness*0.006,0.0);reflectedColor=(reflectedColor*2.0+SampleFace(selected,clamp(uv+d,0.0,1.0))+SampleFace(selected,clamp(uv-d,0.0,1.0))+SampleFace(selected,clamp(uv+d.yx,0.0,1.0))+SampleFace(selected,clamp(uv-d.yx,0.0,1.0)))/6.0;}
          float edge=min(min(planeUv.x,planeUv.y),min(1.0-planeUv.x,1.0-planeUv.y));float edgeFade=uReflectionEdgeFade>0.00001?clamp(edge/uReflectionEdgeFade,0.0,1.0):1.0;
          float visibility=uHasReflectionVisibility!=0?texture(uReflectionVisibility,vUv).r:1.0;float fresnel=pow(1.0-clamp(dot(normalize(uEye-vPosition),normalize(N)),0.0,1.0),uReflectionFresnelPower)*uReflectionFresnelStrength;
          return mix(color,reflectedColor,clamp(uReflectionStrength*visibility*edgeFade*(1.0+fresnel),0.0,1.0));}
        const float PI=3.14159265359;
        void main() {
          vec4 texel=uHasBase!=0?texture(uBase,vUv):vec4(1.0); vec4 base=uColor*vec4(pow(texel.rgb,vec3(2.2)),texel.a);
          if(uCutoff>=0.0&&base.a<uCutoff) discard;
          vec3 emission=uEmissive*(uHasEmission!=0?pow(texture(uEmission,vUv).rgb,vec3(2.2)):vec3(1.0));
          vec3 N=normalize(vNormal); if(!gl_FrontFacing) N=-N; vec3 normalTS=vec3(0.0,0.0,1.0);
          if(uHasNormal!=0) { vec3 dp1=dFdx(vPosition),dp2=dFdy(vPosition); vec2 du1=dFdx(vUv),du2=dFdy(vUv);
            vec3 T=dp1*du2.y-dp2*du1.y; if(dot(T,T)>0.00000001) { T=normalize(T-N*dot(N,T)); vec3 B=normalize(cross(N,T)); normalTS=texture(uNormalMap,vUv).xyz*2.0-1.0; N=normalize(mat3(T,B,N)*normalTS); } }
          vec3 color;
          if(uUnlit!=0) color=base.rgb+emission;
          else {
            vec2 mr=uHasMetalRough!=0?texture(uMetalRough,vUv).bg:vec2(1.0); float metal=clamp(uMetallic*mr.x,0.0,1.0),rough=clamp(uRoughness*mr.y,0.045,1.0);
            vec3 V=normalize(uEye-vPosition),L=normalize(vec3(0.45,0.8,0.35)),H=normalize(V+L);
            float nl=max(dot(N,L),0.0),nv=max(dot(N,V),0.0001),nh=max(dot(N,H),0.0),hv=max(dot(H,V),0.0);
            vec3 f0=mix(vec3(0.04),base.rgb,metal),F=f0+(1.0-f0)*pow(1.0-hv,5.0);
            float a=rough*rough,a2=a*a,d=nh*nh*(a2-1.0)+1.0,D=a2/(PI*d*d),k=(rough+1.0)*(rough+1.0)/8.0;
            float G=(nv/(nv*(1.0-k)+k))*(nl/(nl*(1.0-k)+k));
            vec3 spec=D*G*F/max(4.0*nv*nl,0.0001); float ao=uHasOcclusion!=0?texture(uOcclusion,vUv).r:1.0;
            color=((1.0-F)*(1.0-metal)*base.rgb/PI+spec)*nl*2.2+base.rgb*0.22*ao+emission;
            color=color/(color+vec3(1.0));
          }
          color=ApplyReflection(color,N,normalTS);fragment=vec4(pow(max(color,vec3(0.0)),vec3(1.0/2.2)),base.a);
        }
        """;
}
