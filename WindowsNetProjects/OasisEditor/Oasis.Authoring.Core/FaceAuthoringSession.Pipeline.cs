using OasisEditor.FacePipeline;
using SkiaSharp;

namespace OasisEditor;

public sealed partial class FaceAuthoringSession
{
    /// <summary>Creates a current-schema Face from the selected native Panel2D source shape.</summary>
    public string CreateFromPanelFaceSourceShape(string sourceShapeId, string title,
        double? targetAspectRatio = null, FaceGenerationSettingsModel? settings = null,
        Action<double, string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceShapeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        EnsureCanReplace();
        var (projectFile, projectRoot) = GetProjectContext();
        _project.EnsurePanelMatchesSavedFile();
        var panel = _project.RequirePanelDocument();
        var shape = panel.FaceSourceShapes.FirstOrDefault(value => string.Equals(value.Id, sourceShapeId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Panel2D Face Source Shape '{sourceShapeId}' was not found.");
        var safeName = _assetPaths.SanitizePathSegment(title);
        var package = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.Combine("Assets", "Faces", safeName));
        var generated = WorkspacePathPolicy.ResolveConfined(projectRoot, Path.Combine("Generated", "Faces", safeName));
        if (Directory.Exists(package) || File.Exists(package) || Directory.Exists(generated) || File.Exists(generated))
            throw new IOException($"Face package or generated output '{safeName}' already exists.");
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePanelPath = _project.PanelPath ?? throw new InvalidOperationException("Save the source Panel2D document before generating a Face.");
        var result = new FaceGenerationService().GenerateFromPanelFaceSourceShape(
            panel, shape, safeName, targetAspectRatio: targetAspectRatio,
            projectDirectory: projectRoot, generatedDirectory: Path.Combine(projectRoot, "Generated"),
            faceAssetName: safeName, faceAssetDirectory: package,
            generationSettings: settings, progress: progress,
            sourcePanel2DDocumentPath: ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(projectRoot, sourcePanelPath)),
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = Path.Combine(package, ProjectAssetPathService.FaceManifestFileName);
        Directory.CreateDirectory(package);
        WriteAtomic(manifest, FaceDocumentStorage.Serialize(result.Document), overwrite: false);
        _projectFileAtOpen = projectFile;
        _projectRootAtOpen = projectRoot;
        _facePath = manifest;
        _packageDirectory = package;
        _savedPackageFiles = FingerprintPackage(projectRoot, package);
        var file = FaceDocumentStorage.ToFile(result.Document);
        _savedPayload = FaceDocumentStorage.SerializePayload(file);
        ResetHistory(file);
        _selectedId = null;
        Changed?.Invoke();
        return manifest;
    }

    /// <summary>Imports an authored image, rectifies it through the Face pipeline and builds all artwork stages.</summary>
    public string ImportArtwork(string sourcePath)
    {
        var document = RequireDocument();
        var project = _project.RequireEditorProject();
        var source = ResolveImportSource(sourcePath);
        var title = document.Title ?? "Face";
        var safe = _assetPaths.SanitizePathSegment(title);
        EnsureGeneratedTargetsAvailable(project.ProjectDirectory, safe, allowTrackedFiles: false);
        EnsurePackageUnchangedBeforeAssetImport(project.ProjectDirectory);
        var before = new Dictionary<string, string>(_savedPackageFiles, StringComparer.OrdinalIgnoreCase);
        var imported = FaceArtworkImageImportService.Import(source, project, title);
        ExtendPackageFingerprintAfterImport(project.ProjectDirectory, before, imported.AssetPath);
        var artwork = FaceArtworkImageImportService.CreateArtwork(imported, document.Title ?? "Face");
        var correctionPath = FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(artwork.OutputAssetPath!);
        var correction = new FaceArtworkRebuildService().RebuildImageCorrectionInput(artwork, project.ProjectDirectory,
            correctionPath, FaceDocumentStorage.ToModel(document).GenerationSettings)
            ?? throw new InvalidOperationException("Artwork source could not be rectified into the generated Correction Input.");
        artwork = CopyArtwork(artwork, correctionInput: correction);
        artwork = BuildArtworkStages(artwork, project.ProjectDirectory);
        CaptureGeneratedFaceFiles(project.ProjectDirectory, safe);
        var faceRegion = FaceSourceRegionModel.FromBounds(0, 0, artwork.OutputWidth, artwork.OutputHeight);
        var elementFiles = (document.Elements ?? []).ToArray();
        var artworkElementIndex = Array.FindIndex(elementFiles,
            element => string.Equals(element.Kind, "artwork", StringComparison.OrdinalIgnoreCase));
        var artworkElement = artworkElementIndex >= 0
            ? elementFiles[artworkElementIndex] with
            {
                AssetPath = artwork.OutputAssetPath,
                Width = artwork.OutputWidth,
                Height = artwork.OutputHeight
            }
            : new FaceElementFile
            {
                ObjectId = $"face-artwork-{Guid.NewGuid():N}",
                Name = "Face artwork",
                Kind = "artwork",
                X = 0,
                Y = 0,
                Width = artwork.OutputWidth,
                Height = artwork.OutputHeight,
                IsVisible = true,
                LockTransform = true,
                AssetPath = artwork.OutputAssetPath
            };
        if (artworkElementIndex >= 0) elementFiles[artworkElementIndex] = artworkElement;
        else elementFiles = [artworkElement, .. elementFiles];
        var updated = document with
        {
            Artwork = FaceDocumentStorage.ToFile(new FaceDocumentModel { Artwork = artwork }).Artwork,
            SourceRegion = FaceDocumentStorage.ToFile(new FaceDocumentModel { SourceRegion = faceRegion }).SourceRegion,
            Elements = elementFiles
        };
        Commit(updated);
        return imported.AssetPath;
    }

    public string ImportArtworkOverride(string sourcePath)
    {
        var document = RequireDocument();
        var artwork = FaceDocumentStorage.ToModel(document).Artwork
            ?? throw new InvalidOperationException("Import Face artwork before adding an artwork override.");
        var project = _project.RequireEditorProject();
        var source = ResolveImportSource(sourcePath);
        EnsurePackageUnchangedBeforeAssetImport(project.ProjectDirectory);
        var before = new Dictionary<string, string>(_savedPackageFiles, StringComparer.OrdinalIgnoreCase);
        var value = FaceArtworkOverrideAssetService.Import(source, project, document.Title ?? "Face", artwork.Override);
        ExtendPackageFingerprintAfterImport(project.ProjectDirectory, before, value.AssetPath);
        UpdateArtworkRecipe(CopyArtwork(artwork, artworkOverride: value, replaceOverride: true));
        return value.AssetPath;
    }

    /// <summary>Updates registration, image processing, or override settings as one undoable document command.</summary>
    public bool UpdateArtworkRecipe(FaceArtworkModel artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        var document = RequireDocument();
        if (document.Artwork is null) throw new InvalidOperationException("Import artwork before editing its recipe.");
        ValidateArtworkRecipe(artwork);
        var next = document with { Artwork = FaceDocumentStorage.ToFile(new FaceDocumentModel { Artwork = artwork }).Artwork };
        if (FaceDocumentStorage.SerializePayload(next) == FaceDocumentStorage.SerializePayload(document)) return false;
        Commit(next);
        return true;
    }

    public bool SetArtworkRegistration(FacePerspectiveRegistrationModel registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var artwork = CurrentArtwork ?? throw new InvalidOperationException("Import artwork before editing registration.");
        var normalized = registration.Normalize();
        if (!normalized.IsValid()) throw new ArgumentException("Registration must be a valid convex quadrilateral.", nameof(registration));
        int? width = null, height = null;
        if (artwork.Source.Kind == FaceArtworkSourceKind.Image)
        {
            var size = FaceSourceShapeTransformService.EstimateRegisteredImageOutputSize(artwork.Source.PixelWidth, artwork.Source.PixelHeight, normalized);
            width = size.Width; height = size.Height;
        }
        return UpdateArtworkRecipe(CopyArtwork(artwork, outputWidth: width, outputHeight: height,
            geometry: new FaceArtworkGeometryModel { PerspectiveRegistration = normalized }));
    }

    public bool SetArtworkCalibration(ArtworkCalibrationOperationModel? calibration)
    {
        var artwork = CurrentArtwork ?? throw new InvalidOperationException("Import artwork before editing processing settings.");
        if (calibration is not null && (!double.IsFinite(calibration.Strength) || calibration.Strength is < 0 or > 100))
            throw new ArgumentOutOfRangeException(nameof(calibration), "Calibration strength must be between zero and one hundred.");
        var operations = artwork.ProcessingPipeline.Operations.Where(operation => operation is not ArtworkCalibrationOperationModel).ToList();
        if (calibration is not null) operations.Add(calibration.Normalize());
        return UpdateArtworkRecipe(CopyArtwork(artwork, pipeline: new ImageProcessingPipelineModel { Operations = operations }));
    }

    public bool SetArtworkOverrideAlignment(double x, double y, double width, double height,
        FaceArtworkOverrideAlphaSource alphaSource, FacePerspectiveRegistrationModel registration, bool enabled = true)
    {
        var artwork = CurrentArtwork ?? throw new InvalidOperationException("Import artwork before editing an override.");
        var current = artwork.Override ?? throw new InvalidOperationException("Import an artwork override image first.");
        var value = new FaceArtworkOverrideModel
        {
            Enabled = enabled, AssetPath = current.AssetPath, PixelWidth = current.PixelWidth, PixelHeight = current.PixelHeight,
            AlphaSource = alphaSource, PerspectiveRegistration = registration.Normalize(), X = x, Y = y,
            Width = width, Height = height, ContentRevision = current.ContentRevision
        };
        return UpdateArtworkRecipe(CopyArtwork(artwork, artworkOverride: value, replaceOverride: true));
    }

    /// <summary>Rebuilds Correction Input, Base Artwork and final Output from the saved Face recipe.</summary>
    public void RebuildArtwork(CancellationToken cancellationToken = default)
    {
        var document = RequireDocument();
        var artworkFile = document.Artwork ?? throw new InvalidOperationException("Import artwork before rebuilding it.");
        var artwork = FaceDocumentStorage.ToModel(document).Artwork!;
        var projectDirectory = _project.RequireProjectDirectory();
        ValidateArtworkRecipe(artwork);
        EnsureGeneratedOutputState(artwork, projectDirectory, allowTrackedFiles: true);
        cancellationToken.ThrowIfCancellationRequested();
        string? correction;
        if (artwork.Source.Kind == FaceArtworkSourceKind.Image)
        {
            correction = new FaceArtworkRebuildService().RebuildImageCorrectionInput(artwork, projectDirectory,
                artwork.CorrectionInputAssetPath ?? FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(artwork.OutputAssetPath!),
                FaceDocumentStorage.ToModel(document).GenerationSettings);
        }
        else
        {
            var (panel, shape) = LoadSourcePanelAndShape(artwork, projectDirectory);
            correction = new FaceArtworkRebuildService().RebuildCorrectionInput(artwork, panel, shape, projectDirectory,
                artwork.CorrectionInputAssetPath ?? FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(artwork.OutputAssetPath!),
                FaceDocumentStorage.ToModel(document).GenerationSettings);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (correction is null) throw new InvalidOperationException("The source artwork could not be rectified. Check the source image or Panel2D background and registration.");
        artwork = CopyArtwork(artwork, correctionInput: correction);
        artwork = BuildArtworkStages(artwork, projectDirectory);
        artworkFile = FaceDocumentStorage.ToFile(new FaceDocumentModel { Artwork = artwork }).Artwork!;
        var rebuilt = document with { Artwork = artworkFile };
        var changed = FaceDocumentStorage.SerializePayload(rebuilt) != FaceDocumentStorage.SerializePayload(document);
        Commit(rebuilt);
        CaptureGeneratedFaceFiles(projectDirectory, document.Title ?? "Face");
        if (!changed) Changed?.Invoke();
    }

    private FaceArtworkModel BuildArtworkStages(FaceArtworkModel artwork, string projectDirectory)
    {
        var service = new FaceArtworkRebuildService();
        var baseResult = service.BuildBaseFromCorrectionInput(artwork, projectDirectory);
        if (!baseResult.Succeeded) throw new InvalidOperationException(baseResult.ErrorMessage);
        var outputResult = service.FinalizeOutput(artwork, projectDirectory);
        if (!outputResult.Succeeded) throw new InvalidOperationException(outputResult.ErrorMessage);
        var outputPath = FaceArtworkGeneratedPathService.Resolve(artwork.OutputAssetPath!, projectDirectory);
        using var bitmap = SKBitmap.Decode(outputPath) ?? throw new InvalidDataException("Generated Face artwork could not be read back.");
        return CopyArtwork(artwork, finalWidth: bitmap.Width, finalHeight: bitmap.Height);
    }

    private static FaceArtworkModel CopyArtwork(FaceArtworkModel source, string? correctionInput = null,
        int? outputWidth = null, int? outputHeight = null, int? finalWidth = null, int? finalHeight = null,
        FaceArtworkGeometryModel? geometry = null, ImageProcessingPipelineModel? pipeline = null,
        FaceArtworkOverrideModel? artworkOverride = null, bool replaceOverride = false) => new()
    {
        Id = source.Id, Source = source.Source, Geometry = geometry ?? source.Geometry,
        ProcessingPipeline = pipeline ?? source.ProcessingPipeline,
        CorrectionInputAssetPath = correctionInput ?? source.CorrectionInputAssetPath,
        BaseAssetPath = source.BaseAssetPath, OutputAssetPath = source.OutputAssetPath,
        OutputWidth = outputWidth ?? source.OutputWidth, OutputHeight = outputHeight ?? source.OutputHeight,
        FinalOutputWidth = finalWidth ?? source.FinalOutputWidth, FinalOutputHeight = finalHeight ?? source.FinalOutputHeight,
        Override = replaceOverride ? artworkOverride : source.Override
    };

    private void ValidateArtworkRecipe(FaceArtworkModel artwork)
    {
        if (!Enum.IsDefined(artwork.Source.Kind)) throw new ArgumentOutOfRangeException(nameof(artwork), "Artwork source kind is unsupported.");
        if (!artwork.Geometry.PerspectiveRegistration.Normalize().IsValid()) throw new ArgumentException("Artwork perspective registration must be a valid convex quadrilateral.", nameof(artwork));
        if (artwork.ProcessingPipeline.Operations.Any(operation => operation is not ArtworkCalibrationOperationModel))
            throw new ArgumentException("The artwork processing pipeline contains an unsupported operation.", nameof(artwork));
        foreach (var operation in artwork.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>())
            if (!double.IsFinite(operation.Strength) || operation.Strength is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(artwork), "Calibration strength must be between zero and one hundred.");
        if (artwork.Override is { Enabled: true } value && !value.IsValid())
            throw new ArgumentException("Enabled artwork override settings are incomplete or invalid.", nameof(artwork));
        var root = _project.RequireProjectDirectory();
        foreach (var asset in new[] { artwork.Source.AssetPath, artwork.Source.Panel2DDocumentPath,
                     artwork.CorrectionInputAssetPath, artwork.BaseAssetPath, artwork.OutputAssetPath, artwork.Override?.AssetPath }
                 .Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var relative = Path.IsPathRooted(asset!) ? Path.GetRelativePath(root, asset!) : asset!;
            var resolved = WorkspacePathPolicy.ResolveConfined(root, relative, requireExists: false);
            WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: File.Exists(resolved));
        }
    }

    private string ResolveImportSource(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullPath = Path.GetFullPath(sourcePath, _project.WorkspacePath);
        var relative = Path.GetRelativePath(_project.WorkspacePath, fullPath);
        var resolved = WorkspacePathPolicy.ResolveConfined(_project.WorkspacePath, relative, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(_project.WorkspacePath, relative, requireExists: true);
        if (!File.Exists(resolved)) throw new FileNotFoundException("The workspace artwork image was not found.", resolved);
        return resolved;
    }

    private void EnsureGeneratedTargetsAvailable(string projectDirectory, string faceName, bool allowTrackedFiles)
    {
        var output = Path.Combine("Generated", "Faces", faceName, "Artwork", FaceArtworkGeneratedPathService.OutputFileName);
        var artwork = new FaceArtworkModel
        {
            OutputAssetPath = output,
            BaseAssetPath = FaceArtworkGeneratedPathService.GetBasePathFromOutput(output),
            CorrectionInputAssetPath = FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(output)
        };
        EnsureGeneratedOutputState(artwork, projectDirectory, allowTrackedFiles);
    }

    private void EnsureGeneratedOutputState(FaceArtworkModel artwork, string projectDirectory, bool allowTrackedFiles)
    {
        var current = FingerprintGeneratedFaceFiles(projectDirectory, Current?.Title ?? "Face");
        if (!SameFiles(current, _savedGeneratedFiles))
            throw new IOException("Generated Face artwork changed outside this session; refusing to overwrite its processing files.");
        foreach (var path in new[] { artwork.CorrectionInputAssetPath, artwork.BaseAssetPath, artwork.OutputAssetPath }.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var relative = Path.IsPathRooted(path!) ? Path.GetRelativePath(projectDirectory, path!) : path!;
            var resolved = WorkspacePathPolicy.ResolveConfined(projectDirectory, relative, requireExists: false);
            WorkspacePathPolicy.RejectReparsePoints(projectDirectory, relative, requireExists: File.Exists(resolved));
            if (File.Exists(resolved) && (!allowTrackedFiles || !_savedGeneratedFiles.ContainsKey(Path.GetRelativePath(projectDirectory, resolved).Replace('\\', '/'))))
                throw new IOException($"Generated artwork target '{relative}' already exists and is not owned by the open Face session.");
        }
    }

    private void RebuildCurrentArtworkForHistory()
    {
        if (Current?.Artwork is null) return;
        var projectDirectory = _project.RequireProjectDirectory();
        var face = FaceDocumentStorage.ToModel(Current);
        var model = face.Artwork!;
        EnsureGeneratedOutputState(model, projectDirectory, allowTrackedFiles: true);
        RebuildArtworkStagesForCurrent(model, projectDirectory, face.GenerationSettings);
        CaptureGeneratedFaceFiles(projectDirectory, Current.Title ?? "Face");
    }

    private void RebuildArtworkStagesForCurrent(FaceArtworkModel artwork, string projectDirectory, FaceGenerationSettingsModel? settings)
    {
        string? correction;
        if (artwork.Source.Kind == FaceArtworkSourceKind.Image)
            correction = new FaceArtworkRebuildService().RebuildImageCorrectionInput(artwork, projectDirectory,
                artwork.CorrectionInputAssetPath ?? FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(artwork.OutputAssetPath!), settings);
        else
        {
            var (panel, shape) = LoadSourcePanelAndShape(artwork, projectDirectory);
            correction = new FaceArtworkRebuildService().RebuildCorrectionInput(artwork, panel, shape, projectDirectory,
                artwork.CorrectionInputAssetPath ?? FaceArtworkGeneratedPathService.GetCorrectionInputPathFromOutput(artwork.OutputAssetPath!), settings);
        }
        if (correction is null) throw new InvalidOperationException("The saved artwork recipe could not be rebuilt.");
        BuildArtworkStages(CopyArtwork(artwork, correctionInput: correction), projectDirectory);
    }

    private void EnsurePackageUnchangedBeforeAssetImport(string projectDirectory)
    {
        var packageDirectory = _packageDirectory ?? throw new InvalidOperationException("No Face package is open.");
        var current = FingerprintPackage(projectDirectory, packageDirectory);
        if (!SameFiles(current, _savedPackageFiles))
            throw new IOException("Face package changed externally; refusing to import artwork into it.");
    }

    private void ExtendPackageFingerprintAfterImport(string projectDirectory, IReadOnlyDictionary<string, string> before, string importedAssetPath)
    {
        var packageDirectory = _packageDirectory ?? throw new InvalidOperationException("No Face package is open.");
        var expected = new Dictionary<string, string>(before, StringComparer.OrdinalIgnoreCase);
        var fullAssetPath = WorkspacePathPolicy.ResolveConfined(projectDirectory, importedAssetPath, requireExists: true);
        var key = Path.GetRelativePath(packageDirectory, fullAssetPath).Replace('\\', '/');
        if (!expected.ContainsKey(key)) expected[key] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fullAssetPath)));
        var actual = FingerprintPackage(projectDirectory, packageDirectory);
        if (!SameFiles(actual, expected))
        {
            if (!before.ContainsKey(key)) File.Delete(fullAssetPath);
            throw new IOException("Face package changed during artwork import; the imported file was rolled back.");
        }
        _savedPackageFiles = expected;
    }

    private void CaptureGeneratedFaceFiles(string projectDirectory, string faceName)
        => _savedGeneratedFiles = FingerprintGeneratedFaceFiles(projectDirectory, faceName);

    private static Dictionary<string, string> FingerprintGeneratedFaceFiles(string projectDirectory, string faceName)
    {
        var safeName = new ProjectAssetPathService().SanitizePathSegment(faceName);
        var relativeRoot = Path.Combine("Generated", "Faces", safeName);
        var root = WorkspacePathPolicy.ResolveConfined(projectDirectory, relativeRoot, requireExists: false);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return result;
        WorkspacePathPolicy.RejectReparsePoints(projectDirectory, relativeRoot, requireExists: true);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectDirectory, file);
            WorkspacePathPolicy.RejectReparsePoints(projectDirectory, relative, requireExists: true);
            result[relative.Replace('\\', '/')] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        }
        return result;
    }

    private (Panel2DDocumentModel Panel, PanelFaceSourceShapeModel Shape) LoadSourcePanelAndShape(FaceArtworkModel artwork, string projectDirectory)
    {
        var source = artwork.Source;
        var sourcePath = source.Panel2DDocumentPath ?? Current?.SourcePanel2DDocumentPath;
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new InvalidOperationException("The Face does not retain its source Panel2D document path.");
        var relativePath = Path.IsPathRooted(sourcePath) ? Path.GetRelativePath(projectDirectory, sourcePath) : sourcePath;
        var fullPath = WorkspacePathPolicy.ResolveConfined(projectDirectory, relativePath, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(projectDirectory, relativePath, requireExists: true);
        var currentPath = _project.PanelPath;
        Panel2DDocumentModel panel;
        if (string.Equals(currentPath is null ? null : Path.GetFullPath(currentPath), fullPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            _project.EnsurePanelMatchesSavedFile();
            panel = _project.RequirePanelDocument();
        }
        else panel = ReadPanel(fullPath);
        var shapeId = source.FaceSourceShapeId ?? Current?.SourceFaceShapeId;
        var shape = panel.FaceSourceShapes.FirstOrDefault(value => string.Equals(value.Id, shapeId, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Source Face Shape '{shapeId}' could not be found in '{sourcePath}'.");
        return (panel, shape);
    }

    private static Panel2DDocumentModel ReadPanel(string path)
    {
        if (!Panel2DDocumentStorage.TryReadValidated(File.ReadAllText(path), out var file, out var error))
            throw new InvalidDataException(error);
        return Panel2DDocumentStorage.ToModel(file);
    }
}
