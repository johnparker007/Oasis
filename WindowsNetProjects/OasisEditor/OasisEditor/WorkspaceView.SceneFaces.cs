using System.Numerics;
using System.Runtime.InteropServices;
using Oasis.Rendering3D;
using OasisEditor.Avalonia.Rendering3D;
using OasisEditor.Rendering;
using SkiaSharp;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private readonly Dictionary<string, FaceDocumentModel> _mountedFaces = [];
    private readonly Dictionary<string, int> _faceOutputKeys = [];
    private readonly Face2DRenderer _sceneFaceRenderer = new();
    private readonly Dictionary<string, SceneTexture> _reflectionBaseTextures = [], _reflectionLampTextures = [];
    internal string? ResolveScenePreviewInput(SceneHit? hit)
    {
        if (Preview.State != EmulationBackendState.Running || hit is null || !_mountedFaces.TryGetValue(hit.Id, out var face)) return null;
        var point = new global::Avalonia.Point(hit.TextureCoordinate.X * (face.SourceRegion?.Width ?? face.Artwork?.OutputWidth ?? 1024),
            hit.TextureCoordinate.Y * (face.SourceRegion?.Height ?? face.Artwork?.OutputHeight ?? 768));
        foreach (var button in face.Elements.OfType<FaceButtonElement>().Reverse())
            if (button.IsVisible && new global::Avalonia.Rect(button.X,button.Y,button.Width,button.Height).Contains(point)
                && FaceInputTargetResolver.TryGetInputReference(button,out var reference))
                return Preview.Machine?.InputDefinitions.FirstOrDefault(input => MachineInputReference.FromInputId(input.Id).Id == reference.Id)?.Id;
        return null;
    }
    private IReadOnlyList<string> AddFacePlaneMeshes(Portable3DCompositionResult result, List<SceneRenderMesh> meshes)
    {
        var diagnostics = new List<string>();
        _mountedFaces.Clear(); _faceOutputKeys.Clear(); _reflectionBaseTextures.Clear(); _reflectionLampTextures.Clear();
        foreach (var plane in result.FacePlanes)
        {
            try
            {
                var path = _session.ResolveAsset(plane.FaceAssetPath);
                FaceDocumentModel face;
                if (SameScenePath(path, _faces.FacePath) && _faces.Document is { } openFace) face = openFace;
                else
                {
                    if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out var error)) throw new InvalidDataException(error);
                    face = FaceDocumentStorage.ToModel(file);
                }
                var outputs = _previewOutputs?.Capture(null,face) ?? RuntimeOutputSnapshot.Empty;
                var texture = RenderSceneFace(face, outputs);
                var id = "face:" + plane.TargetId;
                if (result.CabinetDocument?.Reflections?.Any(r => r.Settings.Enabled) == true)
                {
                    _reflectionBaseTextures[plane.TargetId] = RenderSceneFace(face,outputs,1);
                    _reflectionLampTextures[plane.TargetId] = RenderSceneFace(face,outputs,2);
                }
                _mountedFaces[id] = face; _faceOutputKeys[id] = outputs.Fingerprint;
                var vertices = plane.Positions.SelectMany((p,i) => new[] {p.X,p.Y,p.Z,plane.Normal.X,plane.Normal.Y,plane.Normal.Z,plane.TextureCoordinates[i].X,plane.TextureCoordinates[i].Y}).ToArray();
                meshes.Add(new(id,plane.TargetId,vertices,plane.TriangleIndices,new(Vector4.One,Vector3.Zero,AlphaMode:"BLEND",BaseTexture:texture,Unlit:true),Matrix4x4.Identity,"face"));
                _meshNodeIds[id] = "target:" + plane.TargetId;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
            { diagnostics.Add($"Face target '{plane.TargetId}': {e.Message}"); }
        }
        return diagnostics;
    }
    private unsafe SceneTexture RenderSceneFace(FaceDocumentModel face,RuntimeOutputSnapshot outputs,int layer=0)
    {
        var logicalWidth=face.SourceRegion?.Width ?? face.Artwork?.OutputWidth ?? 1024;
        var logicalHeight=face.SourceRegion?.Height ?? face.Artwork?.OutputHeight ?? 768;
        if(logicalWidth<=0||logicalHeight<=0)throw new InvalidDataException("Face dimensions must be positive.");
        var scale=Math.Min(1,1024/Math.Max(logicalWidth,logicalHeight));
        var width=Math.Max(1,(int)Math.Ceiling(logicalWidth*scale));var height=Math.Max(1,(int)Math.Ceiling(logicalHeight*scale));
        using var surface=SKSurface.Create(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Premul));
        if(surface is null)throw new InvalidOperationException("Could not allocate the mounted Face texture.");
        ProjectAssetPathResolver.ProjectDirectoryPath=_session.RequireProjectDirectory();
        surface.Canvas.Clear(SKColors.Transparent);surface.Canvas.Scale((float)scale);
        var runtime = PanelViewport.CreateRuntimeState(outputs);
        if(layer==1)_sceneFaceRenderer.RenderStaticBase(surface.Canvas,face,runtime,PanelViewportTransform.Identity);
        else if(layer==2)_sceneFaceRenderer.RenderLampOverlay(surface.Canvas,face,runtime);
        else _sceneFaceRenderer.Render(surface.Canvas,face,runtime,PanelViewportTransform.Identity);
        using var image=surface.Snapshot();var rgba=new byte[width*height*4];
        fixed(byte* p=rgba)if(!image.ReadPixels(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul),(nint)p,width*4,0,0))throw new InvalidOperationException("Could not read mounted Face pixels.");
        return new([],WrapS:33071,WrapT:33071,RgbaPixels:rgba,Width:width,Height:height);
    }
    private void RefreshMountedFaceOutputs()
    {
        if(_sceneDisposed||!Is3DActive||_mountedFaces.Count==0)return;
        var replacements=new Dictionary<string,SceneTexture>();
        foreach(var pair in _mountedFaces)
        {
            var outputs=_previewOutputs?.Capture(null,pair.Value)??RuntimeOutputSnapshot.Empty;
            if(_faceOutputKeys.GetValueOrDefault(pair.Key)==outputs.Fingerprint)continue;
            replacements[pair.Key]=RenderSceneFace(pair.Value,outputs);_faceOutputKeys[pair.Key]=outputs.Fingerprint;
            var target = pair.Key[5..];
            if(_reflectionBaseTextures.ContainsKey(target))
            {
                _reflectionBaseTextures[target]=RenderSceneFace(pair.Value,outputs,1);
                _reflectionLampTextures[target]=RenderSceneFace(pair.Value,outputs,2);
            }
        }
        if(replacements.Count==0)return;
        _baseScene=_baseScene with{Meshes=_baseScene.Meshes.Select(mesh=>replacements.TryGetValue(mesh.Id,out var texture)?mesh with{Material=mesh.Material with{BaseTexture=texture}}:mesh).ToArray()};
        _baseScene=_baseScene with{Meshes=_baseScene.Meshes.Select(mesh=>mesh.Reflection is not { } reflection ? mesh : mesh with {Reflection=reflection with {Sources=reflection.Sources.Select(source=>source with {Texture=_reflectionBaseTextures.GetValueOrDefault(source.SourceSurfaceTargetId)??source.Texture,LampOverlayTexture=_reflectionLampTextures.GetValueOrDefault(source.SourceSurfaceTargetId)??source.LampOverlayTexture}).ToArray()}}).ToArray()};
        UpdateSceneDisplay();
    }
    private void AddAnchorMeshes(Portable3DCompositionResult result,List<SceneRenderMesh> meshes)
    {
        var color=CurrentScenePalette().Selection;
        var size=Math.Max(.015f,result.Bounds.IsEmpty?.04f:(result.Bounds.Maximum-result.Bounds.Minimum).Length()*.01f);
        foreach(var anchor in result.Anchors)
        {
            var cube=SceneRenderAdapter.Bounds("anchor:"+anchor.Id,new(-size),new(size),color,anchor.WorldTransform) with{Kind="anchor"};
            meshes.Add(cube);_meshNodeIds[cube.Id]="anchor:"+anchor.Id;
        }
    }
}
