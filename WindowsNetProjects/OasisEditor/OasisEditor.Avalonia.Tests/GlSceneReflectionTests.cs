using System.Numerics;
using OasisEditor.Avalonia.Rendering3D;
using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("GL scene GPU")]
public sealed class GlSceneReflectionTests
{
    [WindowsGpuFact]
    public void AuthoredPlanarReflectionProjectsTheMatchedFaceTexture()
    {
        using var bitmap = new SKBitmap(2, 2); bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var face = new SceneTexture(data.ToArray(), MinFilter: 9728, MagFilter: 9728);
        var receiver = Quad("receiver", new(0, 0, 1, 1)) with
        {
            Reflection = new SceneReflectionBinding(true, 1, 1, 0, 1, 0, 0, 0, 0,
                [new SceneReflectionSource(new(-1, -1, 2), new(1, 0, 0), new(0, 1, 0), new(0, 0, -1), 2, 2,
                    "face", face)])
        };
        using var renderer = new GlSceneRenderer();
        var frame = renderer.Render(new([receiver], new(-1), new(1), []), new(Vector3.Zero, 4, 0, 0), 64, 64, Vector4.Zero);
        var center = (32 * 64 + 32) * 4;
        Assert.True(frame.BgraPixels[center + 2] > 240, "The face plane texture should replace the receiver color in the reflection region.");
        Assert.True(frame.BgraPixels[center + 1] < 10);
        Assert.Equal(2, frame.Triangles);
    }

    private static SceneRenderMesh Quad(string id, Vector4 color) => new(id, id,
        [-1,-1,0,0,0,1,0,1, 1,-1,0,0,0,1,1,1, 1,1,0,0,0,1,1,0, -1,1,0,0,0,1,0,0],
        [0,1,2,0,2,3], new(color, Vector3.Zero, Unlit: true), Matrix4x4.Identity);
}
