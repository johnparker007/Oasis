using System.Numerics;
using OasisEditor.Avalonia.Rendering3D;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[CollectionDefinition("GL scene GPU", DisableParallelization = true)]
public sealed class GlSceneGpuCollection { }

[Collection("GL scene GPU")]
public sealed class GlSceneRendererTests
{
    private static SceneRenderMesh Quad(string id, float z, SceneMaterial material) => new(id, id,
        [-1,-1,z,0,0,1,0,1, 1,-1,z,0,0,1,1,1, 1,1,z,0,0,1,1,0, -1,1,z,0,0,1,0,0],
        [0,1,2,0,2,3], material, Matrix4x4.Identity);
    [WindowsGpuFact]
    public void RealOffscreenGpuUsesDepthAndReleasesResizeResources()
    {
        using var renderer = new GlSceneRenderer();
        var front = Quad("front", .2f, new(new(0,1,0,1), Vector3.Zero, Unlit: true));
        var back = Quad("back", 0, new(new(1,0,0,1), Vector3.Zero, Unlit: true));
        var scene = new SceneRenderData([front, back], new(-1), new(1), []);
        var camera = new SceneCameraState(Vector3.Zero, 4, 0, 0);
        var frame = renderer.Render(scene, camera, 64, 64, Vector4.Zero);
        var center = (32 * 64 + 32) * 4;
        Assert.Equal((byte)255, frame.BgraPixels[center + 1]);
        Assert.Equal((byte)0, frame.BgraPixels[center + 2]);
        Assert.Equal(4, frame.Triangles); Assert.Equal(2, frame.DrawCalls);
        Assert.Contains("ANGLE", frame.Device);
        var resized = renderer.Render(scene, camera, 96, 48, Vector4.Zero);
        Assert.Equal(96 * 48 * 4, resized.BgraPixels.Length);
        Assert.Equal((byte)255, resized.BgraPixels[(24 * 96 + 48) * 4 + 1]);
    }
    [WindowsGpuFact]
    public void DisposingOneRendererPreservesAnotherLiveRendererContext()
    {
        var scene = new SceneRenderData(
            [Quad("survivor", 0, new(new(0,1,0,1), Vector3.Zero, Unlit: true))],
            new(-1), new(1), []);
        var camera = new SceneCameraState(Vector3.Zero, 4, 0, 0);
        using var rendererA = new GlSceneRenderer();
        var before = rendererA.Render(scene, camera, 64, 64, Vector4.Zero);
        var center = (32 * 64 + 32) * 4;
        Assert.True(before.BgraPixels[center + 1] > 245);

        using (var rendererB = new GlSceneRenderer())
        {
            var other = rendererB.Render(scene, camera, 64, 64, Vector4.Zero);
            Assert.True(other.BgraPixels[center + 1] > 245);
        }

        var after = rendererA.Render(scene, camera, 64, 64, Vector4.Zero);
        Assert.True(after.BgraPixels[center + 1] > 245);
        Assert.Equal((byte)0, after.BgraPixels[center + 2]);
    }
    [WindowsGpuFact]
    public void RealOffscreenGpuPreservesTextureOrientationAndAlphaCutout()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.SetPixel(0, 0, SKColors.Red); bitmap.SetPixel(1, 0, SKColors.Transparent);
        bitmap.SetPixel(0, 1, SKColors.Blue); bitmap.SetPixel(1, 1, SKColors.White);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var texture = new SceneTexture(data.ToArray(), MinFilter: 9728, MagFilter: 9728);
        var front = Quad("texture", .2f, new(Vector4.One, Vector3.Zero, AlphaMode: "MASK", BaseTexture: texture, Unlit: true));
        var back = Quad("back", 0, new(new(0,1,0,1), Vector3.Zero, Unlit: true));
        using var renderer = new GlSceneRenderer();
        var frame = renderer.Render(new([front, back], new(-1), new(1), []), new(Vector3.Zero, 4, 0, 0), 64, 64, Vector4.Zero);
        int Pixel(int x, int y, int channel) => frame.BgraPixels[(y * 64 + x) * 4 + channel];
        Assert.True(Pixel(24, 24, 2) > 245 && Pixel(24, 24, 0) < 10); // Top-left red.
        Assert.True(Pixel(40, 24, 1) > 245 && Pixel(40, 24, 2) < 10); // Transparent texel exposes green behind it.
        Assert.True(Pixel(24, 40, 0) > 245 && Pixel(24, 40, 2) < 10); // Bottom-left blue.
    }
    [Fact]
    public void CameraRayAndOrbitUseTheSameProjectionAndRejectInvalidState()
    {
        var camera = new SceneCameraState(new(2,3,4), 5, .3f, .4f);
        var ray = camera.Ray(0, 0, 1.5f);
        Assert.True(Vector3.Dot(ray.Direction, Vector3.Normalize(camera.Target - camera.Eye)) > .9999f);
        var clip = Vector4.Transform(new Vector4(camera.Target, 1), camera.View * camera.Projection(1.5f));
        Assert.True(Math.Abs(clip.X / clip.W) < .0001 && Math.Abs(clip.Y / clip.W) < .0001);
        Assert.InRange(camera.Orbit(200, 200).Pitch, -1.55f, 1.55f);
        Assert.Throws<ArgumentException>(() => (camera with { Distance = float.NaN }).Validate());
    }
    [WindowsGpuFact]
    public void TransparentSurfacesSortByTheirBoundsInsteadOfAnArbitraryVertex()
    {
        using var renderer = new GlSceneRenderer();
        var front = Quad("front", .5f, new(new(0,1,0,.5f), Vector3.Zero, AlphaMode: "BLEND", Unlit: true));
        for (var i = 0; i < front.Vertices.Length; i += 8) { front.Vertices[i] *= 100; front.Vertices[i + 1] *= 100; }
        var back = Quad("back", 0, new(new(1,0,0,.5f), Vector3.Zero, AlphaMode: "BLEND", Unlit: true));
        var frame = renderer.Render(new([front, back], new(-1), new(1), []), new(Vector3.Zero, 4, 0, 0), 64, 64, Vector4.Zero);
        var pixel = (32 * 64 + 32) * 4;
        Assert.True(frame.BgraPixels[pixel + 1] > frame.BgraPixels[pixel + 2] + 30);
    }
    [WindowsGpuFact]
    public void MirroredMeshesRenderAndPickConsistentlyAndUnloadingClearsTheCapturedFrame()
    {
        HeadlessTestHost.Run(() =>
        {
            var mesh = Quad("mirror", 0, new(new(0,1,0,1), Vector3.Zero, Unlit: true)) with { Transform = Matrix4x4.CreateScale(-1,1,1) };
            using var viewport = new SceneViewport { Width = 64, Height = 64 };
            var window = new global::Avalonia.Controls.Window { Width = 64, Height = 64, Content = viewport };
            try
            {
                window.Show(); viewport.SetScene(new([mesh], new(-1), new(1), [])); viewport.SetCamera(new(Vector3.Zero,4,0,0));
                viewport.Measure(new global::Avalonia.Size(64,64)); viewport.Arrange(new global::Avalonia.Rect(0,0,64,64));
                using var capture = new global::Avalonia.Media.Imaging.RenderTargetBitmap(new global::Avalonia.PixelSize(64,64), new global::Avalonia.Vector(96,96));
                capture.Render(viewport);
                Assert.NotNull(viewport.LastFrame); Assert.Equal("mirror", viewport.Pick(0,0)?.Id);
                Assert.True(viewport.LastFrame.BgraPixels[(32*64+32)*4+1] > 245);
                viewport.SetScene(SceneRenderData.Empty); Assert.Null(viewport.LastFrame);
                capture.Render(viewport); Assert.Null(viewport.LastFrame);
            }
            finally { window.Close(); }
        });
    }
}

public sealed class WindowsGpuFactAttribute : FactAttribute
{
    public WindowsGpuFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Native GPU tests require the verified Windows ANGLE host; other native backends need platform verification."; }
}
