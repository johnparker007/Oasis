using OasisEditor.Progress;

namespace OasisEditor;

internal sealed class FaceRegenerationResult
{
    public FaceRegenerationResult(FaceDocumentModel document, int updatedElementCount, int addedElementCount, int removedGeneratedElementCount, int preservedManualElementCount, FaceGenerationResult generationResult)
    {
        Document = document;
        UpdatedElementCount = updatedElementCount;
        AddedElementCount = addedElementCount;
        RemovedGeneratedElementCount = removedGeneratedElementCount;
        PreservedManualElementCount = preservedManualElementCount;
        GenerationResult = generationResult;
    }

    public FaceDocumentModel Document { get; }
    public int UpdatedElementCount { get; }
    public int AddedElementCount { get; }
    public int RemovedGeneratedElementCount { get; }
    public int PreservedManualElementCount { get; }
    public FaceGenerationResult GenerationResult { get; }
}

internal sealed class FaceRegenerationService
{
    private readonly FaceGenerationService _generationService;
    private readonly FaceTrayAutoAuthoringService _trayAutoAuthoringService = new();

    public FaceRegenerationService(FaceGenerationService? generationService = null)
    {
        _generationService = generationService ?? new FaceGenerationService();
    }

    public FaceRegenerationResult Regenerate(
        FaceDocumentModel existingFace,
        Panel2DDocumentModel sourcePanel,
        IReadOnlyList<InputDefinitionModel>? inputDefinitions = null,
        string? projectDirectory = null,
        string? generatedDirectory = null,
        FaceGenerationSettingsModel? generationSettings = null,
        IEditorProgressReporter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(existingFace);
        ArgumentNullException.ThrowIfNull(sourcePanel);
        progress ??= NoOpEditorProgressReporter.Instance;
        progress.Report(0.0, "Validating source metadata...");

        if (string.IsNullOrWhiteSpace(existingFace.SourcePanel2DDocumentId))
        {
            throw new InvalidOperationException("Face document does not contain SourcePanel2DDocumentId regeneration metadata.");
        }

        if (existingFace.SourceRegion is not { IsValid: true } sourceRegion)
        {
            throw new InvalidOperationException("Face document does not contain a valid SourceRegion regeneration metadata value.");
        }

        var settings = (generationSettings ?? existingFace.GenerationSettings ?? FaceGenerationSettingsModel.Default).Normalize();

        progress.Report(0.15, "Generating replacement Face...");
        FaceGenerationResult generated;
        if (!string.IsNullOrWhiteSpace(existingFace.SourceFaceShapeId))
        {
            var sourceShapeId = existingFace.SourceFaceShapeId.Trim();
            var sourceShape = sourcePanel.FaceSourceShapes.FirstOrDefault(shape =>
                string.Equals(shape.Id, sourceShapeId, StringComparison.OrdinalIgnoreCase));
            if (sourceShape is null)
            {
                throw new InvalidOperationException($"Face source shape '{sourceShapeId}' could not be found in the source Panel2D document.");
            }

            var targetAspectRatio = sourceRegion.Width > 0 && sourceRegion.Height > 0
                ? sourceRegion.Width / sourceRegion.Height
                : (double?)null;
            generated = _generationService.GenerateFromPanelFaceSourceShape(
                sourcePanel,
                sourceShape,
                existingFace.Title,
                existingFace.SourcePanel2DDocumentId,
                existingFace.SourcePanel2DDocumentPath,
                existingFace.AssignedCabinetFaceTargetId,
                targetAspectRatio,
                projectDirectory,
                generatedDirectory,
                settings,
                progress.CreateChild(0.15, 0.45));
        }
        else
        {
            generated = _generationService.GenerateFromPanelRegion(
                sourcePanel,
                sourceRegion,
                existingFace.Title,
                existingFace.SourcePanel2DDocumentId,
                existingFace.SourcePanel2DDocumentPath,
                inputDefinitions ?? [],
                projectDirectory,
                generatedDirectory,
                settings,
                progress.CreateChild(0.15, 0.45));
        }

        progress.Report(0.45, "Correlating regenerated elements...");
        var existingGeneratedByKey = existingFace.Elements
            .Select(element => new KeyValuePair<string, FaceElementModel>(CreateRegenerationKey(element), element))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var regeneratedKeys = new HashSet<string>(StringComparer.Ordinal);
        var mergedElements = new List<FaceElementModel>();
        var updatedElementCount = 0;
        var addedElementCount = 0;

        foreach (var regeneratedElement in generated.Document.Elements)
        {
            var key = CreateRegenerationKey(regeneratedElement);
            if (!string.IsNullOrWhiteSpace(key))
            {
                regeneratedKeys.Add(key);
            }

            if (!string.IsNullOrWhiteSpace(key) && existingGeneratedByKey.TryGetValue(key, out var existingElement))
            {
                mergedElements.Add(PreserveRuntimeIdentity(regeneratedElement, existingElement));
                updatedElementCount++;
                continue;
            }

            mergedElements.Add(regeneratedElement);
            addedElementCount++;
        }

        progress.Report(0.7, "Preserving manual elements/runtime identity...");
        var preservedManualElements = new List<FaceElementModel>();
        var removedGeneratedElementCount = 0;
        foreach (var existingElement in existingFace.Elements)
        {
            var key = CreateRegenerationKey(existingElement);
            if (string.IsNullOrWhiteSpace(key))
            {
                preservedManualElements.Add(existingElement);
                continue;
            }

            if (!regeneratedKeys.Contains(key))
            {
                removedGeneratedElementCount++;
            }
        }

        mergedElements.AddRange(preservedManualElements);
        progress.Report(0.9, "Auto-authoring trays/emitters...");
        var autoAuthored = _trayAutoAuthoringService.AutoAuthor(new FaceDocumentModel { GenerationSettings = settings, MaskLayer = generated.Document.MaskLayer, Elements = mergedElements.ToArray() }, projectDirectory);

        var regeneratedDocument = new FaceDocumentModel
        {
            Id = existingFace.Id,
            Title = existingFace.Title,
            Summary = generated.Document.Summary ?? $"Regenerated from Panel2D source region ({Format(sourceRegion.X)}, {Format(sourceRegion.Y)}, {Format(sourceRegion.Width)}, {Format(sourceRegion.Height)}).",
            SourcePanel2DDocumentId = existingFace.SourcePanel2DDocumentId,
            SourcePanel2DDocumentPath = existingFace.SourcePanel2DDocumentPath,
            SourceFaceShapeId = existingFace.SourceFaceShapeId,
            AssignedCabinetFaceTargetId = existingFace.AssignedCabinetFaceTargetId,
            SourceRegion = generated.Document.SourceRegion ?? sourceRegion,
            LastRegeneratedAtUtc = DateTime.UtcNow,
            GenerationSettings = settings,
            MaskLayer = generated.Document.MaskLayer,
            Trays = autoAuthored.Trays,
            LampEmitters = autoAuthored.Emitters,
            Layers = EnsureFaceMaskLayer(existingFace.Layers.Count > 0 ? existingFace.Layers : generated.Document.Layers),
            Elements = mergedElements.ToArray()
        };

        progress.Report(1.0, "Face regeneration complete.");
        return new FaceRegenerationResult(
            regeneratedDocument,
            updatedElementCount,
            addedElementCount,
            removedGeneratedElementCount,
            preservedManualElements.Count,
            generated);
    }

    private static IReadOnlyList<FaceLayerModel> EnsureFaceMaskLayer(IReadOnlyList<FaceLayerModel> layers)
    {
        if (layers.Any(layer => string.Equals(layer.Id, "layer-face-mask", StringComparison.OrdinalIgnoreCase)))
        {
            return layers;
        }

        return layers
            .Take(1)
            .Concat([new FaceLayerModel { Id = "layer-face-mask", Name = "Face Mask", IsVisible = true }])
            .Concat(layers.Skip(1))
            .ToArray();
    }

    private static FaceElementModel PreserveRuntimeIdentity(FaceElementModel regeneratedElement, FaceElementModel existingElement)
    {
        var machineReference = existingElement.LinkedMachineObjectReference is MachineObjectReference existingReference && !existingReference.IsEmpty
            ? existingReference
            : regeneratedElement.LinkedMachineObjectReference;

        return regeneratedElement switch
        {
            FaceArtworkElement artwork => new FaceArtworkElement
            {
                ObjectId = existingElement.ObjectId,
                Name = artwork.Name,
                X = artwork.X,
                Y = artwork.Y,
                Width = artwork.Width,
                Height = artwork.Height,
                IsVisible = artwork.IsVisible,
                IsLocked = artwork.IsLocked,
                LinkedMachineObjectReference = machineReference,
                LinkedPanel2DElementId = artwork.LinkedPanel2DElementId,
                AssetPath = artwork.AssetPath,
                SourcePanel2DDocumentId = artwork.SourcePanel2DDocumentId,
                SourceRegion = artwork.SourceRegion,
                Provenance = artwork.Provenance
            },
            FaceReelDisplayElement reel => new FaceReelDisplayElement
            {
                ObjectId = existingElement.ObjectId,
                Name = reel.Name,
                X = reel.X,
                Y = reel.Y,
                Width = reel.Width,
                Height = reel.Height,
                IsVisible = reel.IsVisible,
                IsLocked = reel.IsLocked,
                LinkedMachineObjectReference = machineReference,
                LinkedPanel2DElementId = reel.LinkedPanel2DElementId,
                AssetPath = reel.AssetPath,
                Stops = reel.Stops,
                VisibleScale = reel.VisibleScale,
                BandOffset = reel.BandOffset,
                IsReversed = reel.IsReversed
            },
            FaceSevenSegmentDisplayElement sevenSegment => new FaceSevenSegmentDisplayElement
            {
                ObjectId = existingElement.ObjectId,
                Name = sevenSegment.Name,
                X = sevenSegment.X,
                Y = sevenSegment.Y,
                Width = sevenSegment.Width,
                Height = sevenSegment.Height,
                IsVisible = sevenSegment.IsVisible,
                IsLocked = sevenSegment.IsLocked,
                LinkedMachineObjectReference = machineReference,
                LinkedPanel2DElementId = sevenSegment.LinkedPanel2DElementId,
                OnColorHex = sevenSegment.OnColorHex,
                OffColorHex = sevenSegment.OffColorHex,
                ShowDecimalPoint = sevenSegment.ShowDecimalPoint
            },
            FaceAlphaDisplayElement alpha => new FaceAlphaDisplayElement
            {
                ObjectId = existingElement.ObjectId,
                Name = alpha.Name,
                X = alpha.X,
                Y = alpha.Y,
                Width = alpha.Width,
                Height = alpha.Height,
                IsVisible = alpha.IsVisible,
                IsLocked = alpha.IsLocked,
                LinkedMachineObjectReference = machineReference,
                LinkedPanel2DElementId = alpha.LinkedPanel2DElementId,
                SegmentDisplayType = alpha.SegmentDisplayType,
                OnColorHex = alpha.OnColorHex,
                OffColorHex = alpha.OffColorHex,
                ShowDecimalPoint = alpha.ShowDecimalPoint,
                ShowCommaTail = alpha.ShowCommaTail,
                IsReversed = alpha.IsReversed
            },
            FaceButtonElement button => new FaceButtonElement
            {
                ObjectId = existingElement.ObjectId,
                Name = button.Name,
                X = button.X,
                Y = button.Y,
                Width = button.Width,
                Height = button.Height,
                IsVisible = button.IsVisible,
                IsLocked = button.IsLocked,
                LinkedMachineObjectReference = machineReference,
                LinkedPanel2DElementId = button.LinkedPanel2DElementId,
                LinkedInputReference = existingElement is FaceButtonElement existingButton && existingButton.LinkedInputReference is not null
                    ? existingButton.LinkedInputReference
                    : button.LinkedInputReference
            },
            FaceLampWindowElement lamp => new FaceLampWindowElement
            {
                ObjectId = existingElement.ObjectId,
                Name = lamp.Name,
                X = lamp.X,
                Y = lamp.Y,
                Width = lamp.Width,
                Height = lamp.Height,
                IsVisible = lamp.IsVisible,
                IsLocked = lamp.IsLocked,
                LinkedMachineObjectReference = machineReference,
                LinkedPanel2DElementId = lamp.LinkedPanel2DElementId,
                BulbMaskAssetPath = lamp.BulbMaskAssetPath,
                SourceComponentIndex = lamp.SourceComponentIndex,
                SharedSourceSetId = lamp.SharedSourceSetId,
                SharedSourceSetCount = lamp.SharedSourceSetCount,
                SourceBlend = lamp.SourceBlend
            },
            _ => regeneratedElement
        };
    }

    private static string CreateRegenerationKey(FaceElementModel element)
    {
        if (element is FaceArtworkElement artwork)
        {
            if (!string.IsNullOrWhiteSpace(artwork.LinkedPanel2DElementId))
            {
                return $"artwork:{artwork.LinkedPanel2DElementId.Trim()}";
            }

            if (IsGeneratedArtworkRegion(artwork))
            {
                return "artwork:source-region";
            }

            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(element.LinkedPanel2DElementId))
        {
            return string.Empty;
        }

        var prefix = element switch
        {
            FaceButtonElement => "button",
            FaceReelDisplayElement => "reel",
            FaceSevenSegmentDisplayElement => "sevenSegment",
            FaceAlphaDisplayElement => "alpha",
            FaceLampWindowElement => "lamp",
            _ => element.GetType().Name
        };

        return $"{prefix}:{element.LinkedPanel2DElementId.Trim()}";
    }

    private static bool IsGeneratedArtworkRegion(FaceArtworkElement artwork)
    {
        return artwork.Provenance is not null
            || artwork.SourceRegion is not null
            || !string.IsNullOrWhiteSpace(artwork.SourcePanel2DDocumentId);
    }

    private static string Format(double value) => Math.Round(value, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
