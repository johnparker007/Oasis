using System.Windows;

namespace OasisEditor;

internal static class FaceElementFactory
{
    public static FaceLampWindowElement CreateLampWindow(Point documentPoint)
    {
        return new FaceLampWindowElement
        {
            ObjectId = Guid.NewGuid().ToString("N"),
            Name = "Lamp Window",
            X = Math.Round(documentPoint.X, 2),
            Y = Math.Round(documentPoint.Y, 2),
            Width = 80,
            Height = 40,
            IsVisible = true
        };
    }
}

internal static class FaceElementModelUpdater
{
    public static FaceElementModel Apply(FaceElementModel existing, FaceElementModelUpdate update)
    {
        var linkedMachineObjectReference = existing.LinkedMachineObjectReference;
        if (update.HasLinkedMachineObjectReference)
        {
            linkedMachineObjectReference = update.LinkedMachineObjectReference;
        }

        return existing switch
        {
            FaceArtworkElement artwork => new FaceArtworkElement
            {
                ObjectId = existing.ObjectId,
                Name = update.Name ?? existing.Name,
                X = update.X ?? existing.X,
                Y = update.Y ?? existing.Y,
                Width = update.Width ?? existing.Width,
                Height = update.Height ?? existing.Height,
                IsVisible = update.IsVisible ?? existing.IsVisible,
                IsLocked = update.IsLocked ?? existing.IsLocked,
                LinkedMachineObjectReference = linkedMachineObjectReference,
                LinkedPanel2DElementId = update.HasLinkedPanel2DElementId ? update.LinkedPanel2DElementId : existing.LinkedPanel2DElementId,
                AssetPath = artwork.AssetPath,
                SourcePanel2DDocumentId = artwork.SourcePanel2DDocumentId,
                SourceRegion = artwork.SourceRegion,
                Provenance = artwork.Provenance
            },
            FaceReelDisplayElement reelDisplay => new FaceReelDisplayElement
            {
                ObjectId = existing.ObjectId,
                Name = update.Name ?? existing.Name,
                X = update.X ?? existing.X,
                Y = update.Y ?? existing.Y,
                Width = update.Width ?? existing.Width,
                Height = update.Height ?? existing.Height,
                IsVisible = update.IsVisible ?? existing.IsVisible,
                IsLocked = update.IsLocked ?? existing.IsLocked,
                LinkedMachineObjectReference = linkedMachineObjectReference,
                LinkedPanel2DElementId = update.HasLinkedPanel2DElementId ? update.LinkedPanel2DElementId : existing.LinkedPanel2DElementId,
                AssetPath = reelDisplay.AssetPath,
                Stops = reelDisplay.Stops,
                VisibleScale = reelDisplay.VisibleScale,
                BandOffset = reelDisplay.BandOffset,
                IsReversed = reelDisplay.IsReversed
            },
            FaceLampWindowElement => new FaceLampWindowElement
            {
                ObjectId = existing.ObjectId,
                Name = update.Name ?? existing.Name,
                X = update.X ?? existing.X,
                Y = update.Y ?? existing.Y,
                Width = update.Width ?? existing.Width,
                Height = update.Height ?? existing.Height,
                IsVisible = update.IsVisible ?? existing.IsVisible,
                IsLocked = update.IsLocked ?? existing.IsLocked,
                LinkedMachineObjectReference = linkedMachineObjectReference,
                LinkedPanel2DElementId = update.HasLinkedPanel2DElementId ? update.LinkedPanel2DElementId : existing.LinkedPanel2DElementId,
                BulbMaskAssetPath = ((FaceLampWindowElement)existing).BulbMaskAssetPath,
                SourceComponentIndex = ((FaceLampWindowElement)existing).SourceComponentIndex,
                SharedSourceSetId = ((FaceLampWindowElement)existing).SharedSourceSetId,
                SharedSourceSetCount = ((FaceLampWindowElement)existing).SharedSourceSetCount,
                SourceBlend = ((FaceLampWindowElement)existing).SourceBlend
            },
            FaceSevenSegmentDisplayElement sevenSegmentDisplay => new FaceSevenSegmentDisplayElement
            {
                ObjectId = existing.ObjectId,
                Name = update.Name ?? existing.Name,
                X = update.X ?? existing.X,
                Y = update.Y ?? existing.Y,
                Width = update.Width ?? existing.Width,
                Height = update.Height ?? existing.Height,
                IsVisible = update.IsVisible ?? existing.IsVisible,
                IsLocked = update.IsLocked ?? existing.IsLocked,
                LinkedMachineObjectReference = linkedMachineObjectReference,
                LinkedPanel2DElementId = update.HasLinkedPanel2DElementId ? update.LinkedPanel2DElementId : existing.LinkedPanel2DElementId,
                OnColorHex = sevenSegmentDisplay.OnColorHex,
                OffColorHex = sevenSegmentDisplay.OffColorHex,
                ShowDecimalPoint = sevenSegmentDisplay.ShowDecimalPoint
            },
            FaceAlphaDisplayElement alphaDisplay => new FaceAlphaDisplayElement
            {
                ObjectId = existing.ObjectId,
                Name = update.Name ?? existing.Name,
                X = update.X ?? existing.X,
                Y = update.Y ?? existing.Y,
                Width = update.Width ?? existing.Width,
                Height = update.Height ?? existing.Height,
                IsVisible = update.IsVisible ?? existing.IsVisible,
                IsLocked = update.IsLocked ?? existing.IsLocked,
                LinkedMachineObjectReference = linkedMachineObjectReference,
                LinkedPanel2DElementId = update.HasLinkedPanel2DElementId ? update.LinkedPanel2DElementId : existing.LinkedPanel2DElementId,
                SegmentDisplayType = alphaDisplay.SegmentDisplayType,
                OnColorHex = alphaDisplay.OnColorHex,
                OffColorHex = alphaDisplay.OffColorHex,
                ShowDecimalPoint = alphaDisplay.ShowDecimalPoint,
                ShowCommaTail = alphaDisplay.ShowCommaTail,
                IsReversed = alphaDisplay.IsReversed
            },
            FaceButtonElement button => new FaceButtonElement
            {
                ObjectId = existing.ObjectId,
                Name = update.Name ?? existing.Name,
                X = update.X ?? existing.X,
                Y = update.Y ?? existing.Y,
                Width = update.Width ?? existing.Width,
                Height = update.Height ?? existing.Height,
                IsVisible = update.IsVisible ?? existing.IsVisible,
                IsLocked = update.IsLocked ?? existing.IsLocked,
                LinkedMachineObjectReference = linkedMachineObjectReference,
                LinkedPanel2DElementId = update.HasLinkedPanel2DElementId ? update.LinkedPanel2DElementId : existing.LinkedPanel2DElementId,
                LinkedInputReference = linkedMachineObjectReference is MachineObjectReference reference && reference.Kind == MachineObjectKind.Input && !reference.IsEmpty
                    ? new MachineInputReference(reference)
                    : button.LinkedInputReference
            },
            _ => existing
        };
    }
}

internal sealed class FaceElementModelUpdate
{
    public string? Name { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public bool? IsVisible { get; init; }
    public bool? IsLocked { get; init; }
    public bool HasLinkedMachineObjectReference { get; init; }
    public MachineObjectReference? LinkedMachineObjectReference { get; init; }
    public bool HasLinkedPanel2DElementId { get; init; }
    public string? LinkedPanel2DElementId { get; init; }
}

internal static class FaceElementValidation
{
    public static bool IsValidForInspectorUpdate(FaceElementModel element)
    {
        return PanelElementValidation.IsFinite(element.X)
            && PanelElementValidation.IsFinite(element.Y)
            && PanelElementValidation.IsFinite(element.Width)
            && PanelElementValidation.IsFinite(element.Height)
            && element.Width > 0
            && element.Height > 0;
    }
}

internal static class FaceMaskLayerSelectionService
{
    public const string KindToken = "maskLayer";

    public static PanelSelectionInfo ToSelectionInfo(FaceMaskLayerModel maskLayer)
    {
        return new PanelSelectionInfo(
            string.IsNullOrWhiteSpace(maskLayer.Id) ? "face-mask-layer" : maskLayer.Id,
            KindToken,
            0,
            0,
            Math.Max(0, maskLayer.Width),
            Math.Max(0, maskLayer.Height));
    }

    public static bool IsMaskLayerSelection(PanelSelectionInfo selection)
    {
        return string.Equals(selection.Kind, KindToken, StringComparison.Ordinal)
            || string.Equals(selection.ObjectId, "face-mask-layer", StringComparison.Ordinal)
            || string.Equals(selection.ObjectId, "layer-face-mask", StringComparison.Ordinal);
    }
}

internal static class FaceSelectionService
{
    public static PanelSelectionInfo? SelectFromPoint(
        IReadOnlyList<FaceElementModel> elements,
        Point documentPoint,
        PanelSelectionInfo? currentSelection = null)
    {
        foreach (var element in elements.Reverse())
        {
            if (!element.IsVisible || element.IsLocked)
            {
                continue;
            }

            if (documentPoint.X < element.X || documentPoint.Y < element.Y
                || documentPoint.X > element.X + element.Width
                || documentPoint.Y > element.Y + element.Height)
            {
                continue;
            }

            return ToSelectionInfo(element);
        }

        return null;
    }

    public static PanelSelectionInfo ToSelectionInfo(FaceElementModel element)
    {
        return new PanelSelectionInfo(
            element.ObjectId,
            GetKindToken(element),
            element.X,
            element.Y,
            element.Width,
            element.Height);
    }

    public static string GetKindToken(FaceElementModel element)
    {
        return element switch
        {
            FaceArtworkElement => "artwork",
            FaceButtonElement => "button",
            FaceReelDisplayElement => "reelDisplay",
            FaceSevenSegmentDisplayElement => "sevenSegmentDisplay",
            FaceAlphaDisplayElement => "alphaDisplay",
            FaceLampWindowElement => "lampWindow",
            _ => "unknown"
        };
    }
}
