using OasisEditor.FacePipeline;
using SkiaSharp;

namespace OasisEditor;

public sealed partial class FaceAuthoringSession
{
    /// <summary>Import an immutable authored lamp mask into the bound native package.</summary>
    public string ImportLampMask(string sourcePath)
    {
        var document = RequireDocument();
        var root = _project.RequireProjectDirectory();
        var source = ResolveImportSource(sourcePath);
        using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("The lamp-mask image could not be decoded.");
        EnsurePackageUnchangedBeforeAssetImport(root);
        var relative = Path.GetRelativePath(root, Path.Combine(_packageDirectory!, "Illumination", $"lamp-mask-{Guid.NewGuid():N}.png")).Replace('\\', '/');
        var target = WorkspacePathPolicy.ResolveConfined(root, relative);
        WorkspacePathPolicy.RejectReparsePoints(root, relative, requireExists: false);
        var before = new Dictionary<string, string>(_savedPackageFiles, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) data.SaveTo(stream);
        ExtendPackageFingerprintAfterImport(root, before, relative);
        var state = CloneBuildState(document.BuildState);
        MarkBuild(state, FaceGeneratedProduct.LampMask, FaceBuildStatus.Stale);
        MarkBuild(state, FaceGeneratedProduct.Trays, FaceBuildStatus.Stale);
        InvalidateConfigured(state, FaceGeneratedProduct.RuntimeAssets);
        Commit(document with { MaskLayer = new FaceMaskLayerFile
        {
            Id = document.MaskLayer?.Id ?? "face-mask-layer", Name = "Face lamp mask",
            SourceKind = FaceLampMaskSourceKind.AuthoredImage, AuthoredAssetPath = relative,
            AssetPath = $"Generated/Faces/{_assetPaths.SanitizePathSegment(document.Title ?? "Face")}/Illumination/lamp-mask.png",
            Width = bitmap.Width, Height = bitmap.Height
        }, BuildState = state });
        return relative;
    }

    public string CreateArtworkOverrideFromBase()
    {
        var artwork = CurrentArtwork ?? throw new InvalidOperationException("No Face artwork is configured.");
        if (Current!.BuildState.Get(FaceGeneratedProduct.BaseArtwork).Status != FaceBuildStatus.Current)
            throw new InvalidOperationException("Build current Base artwork before creating an override.");
        var root = _project.RequireProjectDirectory();
        var source = WorkspacePathPolicy.ResolveConfined(root, artwork.BaseAssetPath ?? throw new InvalidOperationException("Base artwork is unavailable."), true);
        return ImportArtworkOverride(source);
    }

    public bool UsePanelArtworkSource()
    {
        var document = RequireDocument();
        var current = CurrentArtwork ?? throw new InvalidOperationException("No artwork is configured.");
        if (current.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape) return false;
        var linked = new FaceArtworkModel { Source = new FaceArtworkSourceModel
        { Panel2DDocumentPath = document.SourcePanel2DDocumentPath, FaceSourceShapeId = document.SourceFaceShapeId } };
        var (panel, shape) = LoadSourcePanelAndShape(linked, _project.RequireProjectDirectory());
        var background = panel.Elements.FirstOrDefault(e => e.Kind == PanelElementKind.Background && !string.IsNullOrWhiteSpace(e.AssetPath))
            ?? throw new InvalidOperationException("The saved source Panel2D has no background artwork.");
        var size = FaceSourceShapeTransformService.EstimateOutputSize(shape);
        return UpdateArtworkRecipe(new FaceArtworkModel
        {
            Id = current.Id, Source = new FaceArtworkSourceModel { Kind = FaceArtworkSourceKind.Panel2DFaceSourceShape,
                AssetPath = background.AssetPath, Panel2DDocumentId = document.SourcePanel2DDocumentId,
                Panel2DDocumentPath = document.SourcePanel2DDocumentPath, FaceSourceShapeId = document.SourceFaceShapeId },
            Geometry = new(), ProcessingPipeline = current.ProcessingPipeline,
            CorrectionInputAssetPath = current.CorrectionInputAssetPath, BaseAssetPath = current.BaseAssetPath, OutputAssetPath = current.OutputAssetPath,
            OutputWidth = size.Width, OutputHeight = size.Height, FinalOutputWidth = current.FinalOutputWidth,
            FinalOutputHeight = current.FinalOutputHeight, Override = current.Override
        });
    }

    public void UseDerivedLampMask()
    {
        var document = RequireDocument();
        var linked = new FaceArtworkModel { Source = new FaceArtworkSourceModel { Panel2DDocumentPath = document.SourcePanel2DDocumentPath, FaceSourceShapeId = document.SourceFaceShapeId } };
        _ = LoadSourcePanelAndShape(linked, _project.RequireProjectDirectory());
        var state = CloneBuildState(document.BuildState);
        MarkBuild(state, FaceGeneratedProduct.LampMask, FaceBuildStatus.Stale); MarkBuild(state, FaceGeneratedProduct.Trays, FaceBuildStatus.Stale);
        InvalidateConfigured(state, FaceGeneratedProduct.RuntimeAssets);
        Commit(document with { MaskLayer = new FaceMaskLayerFile { Id = document.MaskLayer?.Id ?? "face-mask-layer", Name = "Face lamp mask", SourceKind = FaceLampMaskSourceKind.Derived,
            AssetPath = $"Generated/Faces/{_assetPaths.SanitizePathSegment(document.Title ?? "Face")}/Illumination/lamp-mask.png", Width = (int)LogicalWidth, Height = (int)LogicalHeight,
            SourcePanel2DDocumentId = document.SourcePanel2DDocumentId }, BuildState = state });
    }

    public bool RemoveArtworkOverride()
    {
        var artwork = CurrentArtwork;
        return artwork?.Override is not null && UpdateArtworkRecipe(CopyArtwork(artwork, replaceOverride: true));
    }

    public bool SetArtworkOverrideEnabled(bool enabled)
    {
        var artwork = CurrentArtwork ?? throw new InvalidOperationException("No artwork is configured.");
        var value = artwork.Override ?? throw new InvalidOperationException("No artwork override is configured.");
        return SetArtworkOverrideAlignment(value.X, value.Y, value.Width, value.Height, value.AlphaSource, value.PerspectiveRegistration, enabled);
    }

    /// <summary>Replace source-derived component edits; callers must explicitly confirm replacement.</summary>
    public void RebuildComponentsFromSource()
    {
        var document = RequireDocument();
        var model = Document;
        var artwork = model.Artwork ?? throw new InvalidOperationException("No Panel2D artwork source is configured.");
        var (panel, shape) = LoadSourcePanelAndShape(artwork, _project.RequireProjectDirectory());
        var width = (int)LogicalWidth;
        var height = (int)LogicalHeight;
        var converted = new FaceSemanticElementConversionService().ConvertSupportedElements(panel, shape, width, height, _project.RequireProjectDirectory())
            .Where(IsComponent).ToArray();
        var fields = FaceDocumentStorage.ToFile(new FaceDocumentModel { Elements = converted }).Elements!;
        var retained = (document.Elements ?? []).Where(e => e.Kind is not ("reelMount" or "button" or "sevenSegmentDisplay" or "alphaDisplay"));
        var state = CloneBuildState(document.BuildState);
        InvalidateConfigured(state, FaceGeneratedProduct.RuntimeAssets);
        Commit(document with { Elements = retained.Concat(fields).ToArray(), BuildState = state });
    }

    private static bool IsComponent(FaceElementModel e) => e is FaceReelMount or FaceButtonElement or FaceSevenSegmentDisplayElement or FaceAlphaDisplayElement;

    /// <summary>Run native artwork, illumination and optional runtime generation as one history entry.
    /// Generated files are backed up and restored on failure; authored package files are never build outputs.</summary>
    public void BuildFace(Func<FaceDocumentModel, FaceDocumentModel>? runtimeExporter = null, CancellationToken cancellationToken = default)
    {
        var original = RequireDocument();
        var root = _project.RequireProjectDirectory();
        EnsurePackageUnchangedBeforeAssetImport(root);
        var name = _assetPaths.SanitizePathSegment(original.Title ?? "Face");
        var generatedRelative = $"Generated/Faces/{name}";
        var generated = WorkspacePathPolicy.ResolveConfined(root, generatedRelative);
        WorkspacePathPolicy.RejectReparsePoints(root, generatedRelative, requireExists: false);
        var fingerprint = FingerprintGeneratedFaceFiles(root, name);
        if (!SameFiles(fingerprint, _savedGeneratedFiles)) throw new IOException("Generated Face files changed externally; reopen the Face before building.");
        // All writable paths must remain under this Face's generated package.
        var model = Document;
        if (model.Artwork is not null) ValidateArtworkRecipe(model.Artwork);
        foreach (var asset in model.Elements.Select(e => e switch { FaceArtworkElement a => a.AssetPath, FaceReelMount r => r.AssetPath, _ => null })
            .Concat(model.Elements.OfType<FaceReelMount>().Select(r => r.ReelLampTransmissionMaskAssetPath)).Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var resolved = WorkspacePathPolicy.ResolveConfined(root, asset!);
            WorkspacePathPolicy.RejectReparsePoints(root, Path.GetRelativePath(root, resolved), File.Exists(resolved));
        }
        foreach (var path in new[] { model.Artwork?.CorrectionInputAssetPath, model.Artwork?.BaseAssetPath, model.Artwork?.OutputAssetPath, model.MaskLayer?.AssetPath }.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var resolved = WorkspacePathPolicy.ResolveConfined(root, path!);
            if (!resolved.StartsWith(generated + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new IOException("Face Build only writes inside its Generated/Faces package.");
            WorkspacePathPolicy.RejectReparsePoints(root, path!, requireExists: false);
        }
        if (runtimeExporter is not null && !string.Equals(Path.GetFileName(_packageDirectory), name, StringComparison.Ordinal))
            throw new InvalidOperationException("The Face title and package name must agree before standalone runtime export.");
        var backup = Path.Combine(root, $".oasis-face-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backup);
        foreach (var file in fingerprint.Keys)
        {
            var source = WorkspacePathPolicy.ResolveConfined(root, file, true);
            var destination = Path.Combine(backup, Path.GetRelativePath(generated, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }
        var historyIndex = _historyIndex;
        var oldHistory = _history.ToArray();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (model.Artwork is not null) { RebuildArtwork(cancellationToken); model = Document; }
            var next = FaceDocumentStorage.ToFile(model);
            var state = CloneBuildState(next.BuildState);
            if (model.Artwork is not null)
                foreach (var product in new[] { FaceGeneratedProduct.ArtworkCorrectionInput, FaceGeneratedProduct.BaseArtwork, FaceGeneratedProduct.ArtworkOutput }) MarkBuild(state, product, FaceBuildStatus.Current);
            if (model.MaskLayer is not null)
            {
                var mask = BuildLampMask(model, root, name);
                next = next with { MaskLayer = FaceDocumentStorage.ToFile(new FaceDocumentModel { MaskLayer = mask }).MaskLayer };
                var authored = new FaceTrayAutoAuthoringService().AutoAuthor(FaceDocumentStorage.ToModel(next), root);
                var illumination = FaceDocumentStorage.ToFile(new FaceDocumentModel { Trays = authored.Trays, LampEmitters = authored.Emitters });
                next = next with { Trays = illumination.Trays, LampEmitters = illumination.LampEmitters };
                MarkBuild(state, FaceGeneratedProduct.LampMask, FaceBuildStatus.Current);
                MarkBuild(state, FaceGeneratedProduct.Trays, FaceBuildStatus.Current);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (runtimeExporter is not null)
            {
                next = FaceDocumentStorage.ToFile(runtimeExporter(FaceDocumentStorage.ToModel(next)));
                MarkBuild(state, FaceGeneratedProduct.RuntimeAssets, FaceBuildStatus.Current);
            }
            cancellationToken.ThrowIfCancellationRequested();
            next = next with { BuildState = state };
            if (!FaceDocumentStorage.TryReadValidated(FaceDocumentStorage.Serialize(next), out _, out var error)) throw new InvalidDataException(error);
            EnsurePackageUnchangedBeforeAssetImport(root);
            // Collapse intermediate artwork generation into a single document edit.
            _history.Clear(); _history.AddRange(oldHistory); _historyIndex = historyIndex;
            Commit(next, generated: true);
            CaptureGeneratedFaceFiles(root, name);
            Changed?.Invoke();
        }
        catch
        {
            _history.Clear(); _history.AddRange(oldHistory); _historyIndex = historyIndex;
            // Only this confined, link-checked generated tree is owned by the build.
            WorkspacePathPolicy.RejectReparsePoints(root, generatedRelative, requireExists: false);
            if (Directory.Exists(generated)) Directory.Delete(generated, true);
            foreach (var file in Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(generated, Path.GetRelativePath(backup, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination);
            }
            _savedGeneratedFiles = fingerprint;
            Changed?.Invoke();
            throw;
        }
        finally { Directory.Delete(backup, true); }
    }

    private FaceMaskLayerModel BuildLampMask(FaceDocumentModel face, string root, string name)
    {
        var mask = face.MaskLayer!;
        var width = face.Artwork is {} a ? (a.FinalOutputWidth > 0 ? a.FinalOutputWidth : a.OutputWidth) : mask.Width;
        var height = face.Artwork is {} b ? (b.FinalOutputHeight > 0 ? b.FinalOutputHeight : b.OutputHeight) : mask.Height;
        if (width <= 0 || height <= 0 || string.IsNullOrWhiteSpace(mask.AssetPath)) throw new InvalidOperationException("The lamp mask needs a generated path and valid output dimensions.");
        var destination = WorkspacePathPolicy.ResolveConfined(root, mask.AssetPath);
        if (mask.SourceKind == FaceLampMaskSourceKind.AuthoredImage)
        {
            var source = WorkspacePathPolicy.ResolveConfined(root, mask.AuthoredAssetPath ?? throw new InvalidOperationException("No authored lamp-mask source is configured."), true);
            WorkspacePathPolicy.RejectReparsePoints(root, Path.GetRelativePath(root, source), true);
            using var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("The authored lamp mask is unreadable.");
            using var normalized = bitmap.Width == width && bitmap.Height == height ? bitmap.Copy() : bitmap.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
            if (normalized is null) throw new InvalidDataException("The authored lamp mask could not be normalized.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var image = SKImage.FromBitmap(normalized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using (var stream = File.Create(destination)) data.SaveTo(stream);
            return new FaceMaskLayerModel { Id = mask.Id, Name = mask.Name, SourceKind = mask.SourceKind, AuthoredAssetPath = mask.AuthoredAssetPath, AssetPath = mask.AssetPath,
                Width = width, Height = height, GeneratedUtc = DateTime.UtcNow, ExtractionThreshold = mask.ExtractionThreshold, Contributions = mask.Contributions,
                SourcePanel2DDocumentId = mask.SourcePanel2DDocumentId, SourceRegion = mask.SourceRegion };
        }
        var artwork = face.Artwork ?? new FaceArtworkModel();
        var (panel, shape) = LoadSourcePanelAndShape(artwork, root);
        var lamps = face.Elements.OfType<FaceLampWindowElement>().Where(w => w.IsVisible).ToArray();
        if (lamps.Length == 0) throw new InvalidOperationException("No visible Face lamp windows are configured.");
        foreach (var lamp in lamps)
        {
            var source = panel.Elements.FirstOrDefault(e => e.ObjectId == lamp.LinkedPanel2DElementId && e.Kind == PanelElementKind.Lamp);
            if (string.IsNullOrWhiteSpace(source?.AssetPath)) throw new InvalidOperationException($"Lamp '{lamp.Name}' has no linked Panel2D artwork.");
            var full = WorkspacePathPolicy.ResolveConfined(root, source.AssetPath, true);
            WorkspacePathPolicy.RejectReparsePoints(root, Path.GetRelativePath(root, full), true);
        }
        return new FaceGenerationService().GenerateMaskFromSourceShape(panel, shape, width, height, lamps, face.Id, face.SourcePanel2DDocumentId,
            root, name, destination, face.GenerationSettings.MaskExtractionThreshold, ImageProcessingExecutionPolicy.Current)
            ?? throw new InvalidOperationException("Lamp-mask generation produced no output.");
    }

    private static void MarkBuild(FaceBuildStateModel state, FaceGeneratedProduct product, FaceBuildStatus status)
    { var node = state.Get(product); node.Status = status; node.ErrorMessage = null; }
}
