using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using SkiaSharp;

namespace OasisEditor.Automation;

public sealed partial class McpEditorSession
{
    private readonly Dictionary<Guid, OpenFace> _faces = [];
    private EditorProject Project => new()
    {
        Name = Path.GetFileNameWithoutExtension(_projectFile!), ProjectFilePath = _projectFile!, ProjectDirectory = ProjectDirectory,
        AssetsDirectory = Path.Combine(ProjectDirectory, "Assets"), GeneratedDirectory = Path.Combine(ProjectDirectory, "Generated")
    };

    public FaceSourceShapeSnapshot AddFaceSourceShape(Guid panelDocumentId, string name, FaceQuad? corners = null)
    {
        ValidateName(name);
        var panel = Get(panelDocumentId).Document;
        var bounds = EditorLogicalViewportBounds.Panel2D(panel);
        corners ??= new(new(bounds.Left, bounds.Top), new(bounds.Right, bounds.Top), new(bounds.Right, bounds.Bottom), new(bounds.Left, bounds.Bottom));
        var shape = MakeShape(name, corners);
        panel.CommandService.Execute(PanelFaceSourceShapeCommands.CreateAddCommand(panelDocumentId, panel, shape));
        return DescribeShape(shape);
    }

    public IReadOnlyList<FaceSourceShapeSnapshot> InspectFaceSourceShapes(Guid panelDocumentId, int offset = 0, int limit = 50)
    {
        ValidatePage(offset, limit);
        return Get(panelDocumentId).Document.GetPanelFaceSourceShapes().Skip(offset).Take(limit).Select(DescribeShape).ToArray();
    }

    public FaceSnapshot CreateFace(string name, string? sourceImage = null)
    {
        var path = PrepareFacePackage(name);
        string? image = sourceImage is null ? null : Confine(sourceImage);
        if (image is not null)
        {
            using var codec = SKCodec.Create(image) ?? throw new InvalidDataException("Source artwork is not a supported image.");
            ValidateOutputSize(codec.Info.Width, codec.Info.Height);
        }
        var result = new FaceDocumentCreationService().CreateFaceDocument(new(name, _faces.Count + 1,
            image is null ? FaceStartingArtworkKind.Blank : FaceStartingArtworkKind.Image, image), Project);
        if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
        var entry = RegisterFace(result.Document!, path);
        return SaveFace(entry.Document.DocumentId);
    }

    public FaceGenerationSnapshot GenerateFaceFromPanel(Guid panelDocumentId, string shapeId, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var panel = Get(panelDocumentId).Document;
        if (panel.IsDirty) throw new InvalidOperationException("Save the source panel, including its source shape, before generating a Face.");
        var shape = panel.GetPanelFaceSourceShapes().FirstOrDefault(s => s.Id == shapeId) ?? throw new ArgumentException("Source shape ID does not exist in the panel.");
        var panelEntry = Get(panelDocumentId);
        if (!File.Exists(panelEntry.Path) || Hash(panelEntry.Path) != panelEntry.SavedHash) throw new IOException("Source panel changed on disk; reopen it before generation.");
        ValidatePanelAssets(panel);
        // Revalidate even persisted shapes before any raster allocation or filesystem writes.
        MakeShape(shape.Name, new(new(shape.TopLeft.X, shape.TopLeft.Y), new(shape.TopRight.X, shape.TopRight.Y),
            new(shape.BottomRight.X, shape.BottomRight.Y), new(shape.BottomLeft.X, shape.BottomLeft.Y)));
        var path = PrepareFacePackage(name);
        var pending = Confine(Path.Combine(Project.GeneratedDirectory, "Faces", "_unsaved", Guid.NewGuid().ToString("N")));
        var previousProject = ProjectAssetPathResolver.ProjectDirectoryPath;
        try
        {
            ProjectAssetPathResolver.ProjectDirectoryPath = ProjectDirectory;
            var generated = new FaceGenerationService().GenerateFromPanelFaceSourceShape(panel.GetPanelDocument(), shape, name,
                sourcePanel2DDocumentId: panelDocumentId.ToString("N"), projectDirectory: ProjectDirectory,
                generatedDirectory: Project.GeneratedDirectory, faceAssetName: name, faceAssetDirectory: pending,
                sourcePanel2DDocumentPath: panel.FilePath, cancellationToken: cancellationToken);
            var document = new DocumentTabViewModel(EditorDocument.CreateFaceStub(name).MarkDirty(), faceDocumentJson: FaceDocumentStorage.Serialize(generated.Document));
            var entry = RegisterFace(document, path);
            return new(DescribeFace(entry), generated.ConvertedLampCount, generated.ConvertedButtonCount,
                generated.ConvertedSevenSegmentDisplayCount, generated.ConvertedAlphaDisplayCount, generated.ConvertedReelMountCount);
        }
        finally { ProjectAssetPathResolver.ProjectDirectoryPath = previousProject; }
    }

    public FaceSnapshot OpenFaceDocument(string path)
    {
        var file = Confine(path);
        var root = Path.Combine(ProjectDirectory, "Assets", "Faces");
        if (Path.GetFileName(file) != ProjectAssetPathService.FaceManifestFileName
            || !string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(file)), root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected Assets/Faces/<Name>/asset.face in the open project.");
        if (_faces.Values.Any(f => string.Equals(f.Path, file, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Face already open; use its document ID.");
        var content = File.ReadAllText(file);
        if (!FaceDocumentStorage.TryReadValidated(content, out _, out var error)) throw new InvalidDataException(error);
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(file, "Face opened through MCP."), faceDocumentJson: content);
        ValidateFacePaths(document.GetFaceDocument());
        return DescribeFace(RegisterFace(document, file));
    }

    public FaceSnapshot InspectFace(Guid documentId, int offset = 0, int limit = 50)
    {
        ValidatePage(offset, limit);
        return DescribeFace(GetFace(documentId), offset, limit);
    }

    public FaceSnapshot SaveFace(Guid documentId)
    {
        var entry = GetFace(documentId);
        ValidateFacePaths(entry.Document.GetFaceDocument());
        ValidateFaceOutputTrees();
        var currentFiles = FingerprintPackage(Path.GetDirectoryName(entry.Path)!);
        if (currentFiles.Count != entry.Files.Count || currentFiles.Any(f => !entry.Files.TryGetValue(f.Key, out var hash) || hash != f.Value))
            throw new IOException("Face package changed externally; refusing to overwrite authored files.");
        new DocumentSaveService().SaveDocument(entry.Document, entry.Path, Project).ApplyTo(entry.Document);
        entry.Files = FingerprintPackage(Path.GetDirectoryName(entry.Path)!);
        return DescribeFace(entry);
    }

    public byte[] CaptureFace(Guid documentId, int width, int height, ThemePreference theme)
    {
        var document = GetFace(documentId).Document;
        ValidateFacePaths(document.GetFaceDocument());
        return McpFaceCapture.Capture(document, ProjectDirectory, width, height, theme);
    }

    private OpenFace GetFace(Guid id) => _faces.TryGetValue(id, out var face) ? face : throw new ArgumentException("Unknown Face document ID for this session.");

    private string PrepareFacePackage(string name)
    {
        ValidateName(name);
        ValidateFaceOutputTrees();
        var directory = Confine(Path.Combine(Project.AssetsDirectory, "Faces", name));
        var generated = Confine(Path.Combine(Project.GeneratedDirectory, "Faces", name));
        if (Directory.Exists(directory) || File.Exists(directory) || Directory.Exists(generated) || File.Exists(generated))
            throw new IOException("Face package or generated output already exists; refusing to overwrite it.");
        return Path.Combine(directory, ProjectAssetPathService.FaceManifestFileName);
    }

    private OpenFace RegisterFace(DocumentTabViewModel document, string path)
    {
        Directory.CreateDirectory(Confine(Path.GetDirectoryName(path)!));
        document.SetProjectAccessor(() => Project);
        document.SetOpenDocumentsAccessor(() => _panels.Values.Select(p => p.Document).Concat(_faces.Values.Select(f => f.Document)).ToArray());
        var entry = new OpenFace(document, path, FingerprintPackage(Path.GetDirectoryName(path)!));
        _faces.Add(document.DocumentId, entry);
        return entry;
    }

    private void ValidateFaceOutputTrees()
    {
        ValidateTree(Confine(Path.Combine(ProjectDirectory, "Assets", "Faces")));
        ValidateTree(Confine(Path.Combine(ProjectDirectory, "Generated", "Faces")));
    }

    private void ValidateTree(string root)
    {
        if (!Directory.Exists(root)) return;
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
            foreach (var child in Directory.EnumerateFileSystemEntries(directory))
            {
                Confine(child);
                if (Directory.Exists(child)) pending.Push(child);
            }
    }

    private Dictionary<string, string> FingerprintPackage(string directory)
    {
        ValidateTree(Confine(directory));
        if (!Directory.Exists(directory)) return new(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(f => f, f =>
        {
            using var stream = File.OpenRead(f);
            return Convert.ToHexString(SHA256.HashData(stream));
        }, StringComparer.OrdinalIgnoreCase);
    }

    private void ValidateFacePaths(FaceDocumentModel face)
    {
        using var json = JsonDocument.Parse(FaceDocumentStorage.Serialize(face));
        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.EndsWith("path", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(property.Value.GetString())) Confine(Path.Combine(ProjectDirectory, property.Value.GetString()!));
                    Visit(property.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) Visit(child);
        }
        Visit(json.RootElement);
    }

    private static PanelFaceSourceShapeModel MakeShape(string name, FaceQuad quad)
    {
        if (quad.TopLeft is null || quad.TopRight is null || quad.BottomRight is null || quad.BottomLeft is null) throw new ArgumentException("All four corners are required.");
        var points = new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft };
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || Math.Abs(p.X) > 1_000_000 || Math.Abs(p.Y) > 1_000_000)) throw new ArgumentException("Source corners must be finite and within +/-1,000,000.");
        double? sign = null;
        for (var i = 0; i < 4; i++)
        {
            var a = points[i]; var b = points[(i + 1) % 4]; var c = points[(i + 2) % 4];
            var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(cross) < 0.000001 || (sign.HasValue && Math.Sign(cross) != Math.Sign(sign.Value))) throw new ArgumentException("Corners must form an ordered, convex, non-degenerate quadrilateral.");
            sign = cross;
        }
        var shape = new PanelFaceSourceShapeModel { Id = "faceSourceShape-" + Guid.NewGuid().ToString("N"), Name = name,
            TopLeft = new() { X = quad.TopLeft.X, Y = quad.TopLeft.Y }, TopRight = new() { X = quad.TopRight.X, Y = quad.TopRight.Y },
            BottomRight = new() { X = quad.BottomRight.X, Y = quad.BottomRight.Y }, BottomLeft = new() { X = quad.BottomLeft.X, Y = quad.BottomLeft.Y } };
        var size = FaceSourceShapeTransformService.EstimateOutputSize(shape);
        ValidateOutputSize(size.Width, size.Height);
        return shape;
    }

    private static void ValidateOutputSize(int width, int height)
    {
        if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new ArgumentException("MCP Face output is limited to 1..4096 pixels per dimension.");
    }

    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0 || limit < 1 || limit > 200) throw new ArgumentOutOfRangeException(nameof(limit), "Use offset >= 0 and limit 1..200.");
    }

    private static FaceSourceShapeSnapshot DescribeShape(PanelFaceSourceShapeModel shape) => new(shape.Id, shape.Name,
        new(new(shape.TopLeft.X, shape.TopLeft.Y), new(shape.TopRight.X, shape.TopRight.Y), new(shape.BottomRight.X, shape.BottomRight.Y), new(shape.BottomLeft.X, shape.BottomLeft.Y)));

    private static FaceSnapshot DescribeFace(OpenFace entry, int offset = 0, int limit = 50)
    {
        var model = entry.Document.GetFaceDocument();
        return new(entry.Document.DocumentId, model.Id, model.Title, entry.Path, entry.Document.IsDirty,
            model.SourcePanel2DDocumentPath, model.SourceFaceShapeId, model.SourceRegion?.Width ?? 0, model.SourceRegion?.Height ?? 0,
            model.Artwork?.OutputAssetPath, model.MaskLayer?.AssetPath, model.Trays.Count, model.LampEmitters.Count,
            model.Elements.Count, offset, model.Elements.Skip(offset).Take(limit).Select(e => new FaceElementSnapshot(e.ObjectId, e.Name,
                e.GetType().Name, e.X, e.Y, e.Width, e.Height, e.IsVisible, e.LinkedPanel2DElementId)).ToArray(),
            model.BuildState.Products.Select(p => new FaceBuildProductSnapshot(p.Key.ToString(), p.Value.Status.ToString(), p.Value.ErrorMessage)).ToArray(),
            Path.Combine(Path.GetDirectoryName(entry.Path)!, ProjectAssetPathService.FaceArtworkFileName),
            Path.Combine(Path.GetDirectoryName(entry.Path)!, ProjectAssetPathService.FaceMaskFileName));
    }

    private sealed class OpenFace(DocumentTabViewModel document, string path, Dictionary<string, string> files)
    {
        public DocumentTabViewModel Document { get; } = document;
        public string Path { get; } = path;
        public Dictionary<string, string> Files { get; set; } = files;
    }
}

public sealed record FacePoint(double X, double Y);
public sealed record FaceQuad(FacePoint TopLeft, FacePoint TopRight, FacePoint BottomRight, FacePoint BottomLeft);
public sealed record FaceSourceShapeSnapshot(string Id, string Name, FaceQuad Corners);
public sealed record FaceSnapshot(Guid DocumentId, string AssetId, string Name, string Path, bool IsDirty, string? SourcePanelPath,
    string? SourceShapeId, double Width, double Height, string? ArtworkPath, string? MaskPath, int TrayCount, int EmitterCount,
    int ElementCount, int Offset, IReadOnlyList<FaceElementSnapshot> Elements, IReadOnlyList<FaceBuildProductSnapshot> BuildProducts,
    string AuthoredArtworkPath, string AuthoredMaskPath);
public sealed record FaceElementSnapshot(string Id, string Name, string Kind, double X, double Y, double Width, double Height, bool IsVisible, string? SourceElementId);
public sealed record FaceBuildProductSnapshot(string Product, string Status, string? Error);
public sealed record FaceGenerationSnapshot(FaceSnapshot Face, int LampCount, int ButtonCount, int SevenSegmentCount, int AlphaCount, int ReelMountCount);
