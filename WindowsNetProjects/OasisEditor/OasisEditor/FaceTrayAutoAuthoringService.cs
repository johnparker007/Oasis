using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace OasisEditor;

internal sealed class FaceTrayAutoAuthoringService
{
    public FaceTrayAutoAuthoringResult AutoAuthor(FaceDocumentModel faceDocument)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);

        var trays = new List<FaceTrayModel>();
        var emitters = new List<FaceLampEmitterElement>();
        var nextTrayNumber = 1;

        foreach (var lampWindow in faceDocument.Elements
            .OfType<FaceLampWindowElement>()
            .Where(IsValidVisibleLampWindow)
            .OrderBy(CreateLampStableKey, StringComparer.Ordinal))
        {
            var contribution = FindBestContribution(lampWindow, faceDocument.MaskLayer);
            var bounds = contribution?.Bounds is { IsValid: true } contributionBounds
                ? contributionBounds
                : FaceSourceRegionModel.FromRect(new Rect(lampWindow.X, lampWindow.Y, lampWindow.Width, lampWindow.Height));
            var source = contribution?.Bounds is { IsValid: true } ? "maskContributionBounds" : "lampWindowBounds";
            var stableKey = CreateLampStableKey(lampWindow);
            var trayObjectId = CreateStableId("face-tray", stableKey, source, Format(bounds.X), Format(bounds.Y), Format(bounds.Width), Format(bounds.Height));
            var trayNumber = nextTrayNumber++;
            var lampId = TryGetLampId(lampWindow.LinkedMachineObjectReference);

            trays.Add(new FaceTrayModel
            {
                ObjectId = trayObjectId,
                Name = string.IsNullOrWhiteSpace(lampWindow.Name)
                    ? $"Auto Tray {lampId?.ToString() ?? trayNumber.ToString()}"
                    : $"{lampWindow.Name.Trim()} Tray",
                IsAutoAuthored = true,
                AutoAuthoringSource = source,
                SourceLampWindowObjectId = lampWindow.ObjectId,
                SourcePanel2DElementId = contribution?.SourcePanel2DElementId ?? lampWindow.LinkedPanel2DElementId,
                LinkedMachineObjectReference = lampWindow.LinkedMachineObjectReference,
                Bounds = bounds,
                Vertices = CreateRectangleVertices(bounds)
            });

            emitters.Add(new FaceLampEmitterElement
            {
                ObjectId = CreateStableId("face-emitter", stableKey),
                Name = string.IsNullOrWhiteSpace(lampWindow.Name)
                    ? $"Lamp {lampId?.ToString() ?? trayNumber.ToString()} Emitter"
                    : $"{lampWindow.Name.Trim()} Emitter",
                X = Math.Round(lampWindow.X, 2),
                Y = Math.Round(lampWindow.Y, 2),
                Width = Math.Round(lampWindow.Width, 2),
                Height = Math.Round(lampWindow.Height, 2),
                IsVisible = lampWindow.IsVisible,
                IsLocked = true,
                LinkedMachineObjectReference = lampWindow.LinkedMachineObjectReference,
                LinkedPanel2DElementId = lampWindow.LinkedPanel2DElementId,
                SourceLampWindowObjectId = lampWindow.ObjectId,
                TrayObjectId = trayObjectId,
                TrayId = trayNumber,
                LampId = lampId,
                CenterX = Math.Round(lampWindow.X + (lampWindow.Width / 2d), 2),
                CenterY = Math.Round(lampWindow.Y + (lampWindow.Height / 2d), 2),
                IsAutoAuthored = true,
                AutoAuthoringSource = source
            });
        }

        return new FaceTrayAutoAuthoringResult(trays, emitters);
    }

    public IReadOnlyList<FaceValidationDiagnostic> Validate(FaceDocumentModel faceDocument)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);

        var diagnostics = new List<FaceValidationDiagnostic>();
        AddDuplicateDiagnostics(faceDocument.Trays.Select(tray => tray.ObjectId), "Face.Tray.DuplicateId", "tray", diagnostics);
        AddDuplicateDiagnostics(faceDocument.LampEmitters.Select(emitter => emitter.ObjectId), "Face.Emitter.DuplicateId", "emitter", diagnostics);

        var trayIds = faceDocument.Trays
            .Where(tray => !string.IsNullOrWhiteSpace(tray.ObjectId))
            .Select(tray => tray.ObjectId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var tray in faceDocument.Trays)
        {
            if (tray.Bounds is not { IsValid: true })
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.Tray.Bounds.Invalid",
                    $"Face tray '{DisplayName(tray.ObjectId, tray.Name)}' has invalid bounds."));
            }

            if (tray.Vertices.Count > 0 && tray.Vertices.Any(vertex => !PanelElementValidation.IsFinite(vertex.X) || !PanelElementValidation.IsFinite(vertex.Y)))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.Tray.Vertices.Invalid",
                    $"Face tray '{DisplayName(tray.ObjectId, tray.Name)}' has invalid polygon vertices."));
            }
        }

        foreach (var emitter in faceDocument.LampEmitters)
        {
            if (string.IsNullOrWhiteSpace(emitter.TrayObjectId) || !trayIds.Contains(emitter.TrayObjectId.Trim()))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.Emitter.TrayReference.Missing",
                    $"Face lamp emitter '{DisplayName(emitter.ObjectId, emitter.Name)}' references missing tray '{emitter.TrayObjectId}'."));
            }

            if (!PanelElementValidation.IsFinite(emitter.CenterX) || !PanelElementValidation.IsFinite(emitter.CenterY))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.Emitter.Position.Invalid",
                    $"Face lamp emitter '{DisplayName(emitter.ObjectId, emitter.Name)}' has an invalid position."));
            }
        }

        return diagnostics;
    }

    private static void AddDuplicateDiagnostics(IEnumerable<string> ids, string code, string noun, List<FaceValidationDiagnostic> diagnostics)
    {
        foreach (var duplicate in ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            diagnostics.Add(new FaceValidationDiagnostic(
                FaceValidationSeverity.Warning,
                code,
                $"Face contains duplicate {noun} ID '{duplicate}'."));
        }
    }

    private static FaceMaskContributionModel? FindBestContribution(FaceLampWindowElement lampWindow, FaceMaskLayerModel? maskLayer)
    {
        if (maskLayer is null || maskLayer.Contributions.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(lampWindow.LinkedPanel2DElementId))
        {
            var linkedPanelElementId = lampWindow.LinkedPanel2DElementId.Trim();
            var contribution = maskLayer.Contributions
                .Where(candidate => candidate.Bounds is { IsValid: true })
                .FirstOrDefault(candidate => string.Equals(candidate.SourcePanel2DElementId, linkedPanelElementId, StringComparison.Ordinal));
            if (contribution is not null)
            {
                return contribution;
            }
        }

        var reference = lampWindow.LinkedMachineObjectReference?.ToString();
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var contribution = maskLayer.Contributions
                .Where(candidate => candidate.Bounds is { IsValid: true })
                .FirstOrDefault(candidate => string.Equals(candidate.LinkedMachineObjectReference?.ToString(), reference, StringComparison.Ordinal));
            if (contribution is not null)
            {
                return contribution;
            }
        }

        var lampBounds = new Rect(lampWindow.X, lampWindow.Y, lampWindow.Width, lampWindow.Height);
        return maskLayer.Contributions
            .Where(candidate => candidate.Bounds is { IsValid: true })
            .OrderByDescending(candidate => OverlapArea(lampBounds, candidate.Bounds!.ToRect()))
            .ThenByDescending(candidate => candidate.PixelCount)
            .FirstOrDefault(candidate => OverlapArea(lampBounds, candidate.Bounds!.ToRect()) > 0d);
    }

    private static IReadOnlyList<FacePointModel> CreateRectangleVertices(FaceSourceRegionModel bounds)
    {
        var left = Math.Round(bounds.X, 2);
        var top = Math.Round(bounds.Y, 2);
        var right = Math.Round(bounds.X + bounds.Width, 2);
        var bottom = Math.Round(bounds.Y + bounds.Height, 2);
        return
        [
            new FacePointModel { X = left, Y = top },
            new FacePointModel { X = right, Y = top },
            new FacePointModel { X = right, Y = bottom },
            new FacePointModel { X = left, Y = bottom }
        ];
    }

    private static bool IsValidVisibleLampWindow(FaceLampWindowElement element)
    {
        return element.IsVisible && element.Width > 0d && element.Height > 0d;
    }

    private static int? TryGetLampId(MachineObjectReference? reference)
    {
        return reference is MachineObjectReference { Kind: MachineObjectKind.Lamp } lampReference
            && int.TryParse(lampReference.Id, out var id)
            ? id
            : null;
    }

    private static double OverlapArea(Rect left, Rect right)
    {
        var intersection = Rect.Intersect(left, right);
        return intersection.IsEmpty ? 0d : intersection.Width * intersection.Height;
    }

    private static string CreateLampStableKey(FaceLampWindowElement lampWindow)
    {
        if (!string.IsNullOrWhiteSpace(lampWindow.LinkedPanel2DElementId))
        {
            return $"panel:{lampWindow.LinkedPanel2DElementId.Trim()}";
        }

        var reference = lampWindow.LinkedMachineObjectReference?.ToString();
        if (!string.IsNullOrWhiteSpace(reference))
        {
            return $"machine:{reference}";
        }

        return $"bounds:{Format(lampWindow.X)}:{Format(lampWindow.Y)}:{Format(lampWindow.Width)}:{Format(lampWindow.Height)}:{lampWindow.Name}";
    }

    private static string CreateStableId(string prefix, params string[] parts)
    {
        var input = string.Join("|", parts.Select(part => part.Trim()));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var hash = Convert.ToHexString(bytes[..8]).ToLowerInvariant();
        return $"{prefix}-{hash}";
    }

    private static string DisplayName(string objectId, string name)
    {
        return string.IsNullOrWhiteSpace(name) ? objectId : name.Trim();
    }

    private static string Format(double value) => Math.Round(value, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed record FaceTrayAutoAuthoringResult(
    IReadOnlyList<FaceTrayModel> Trays,
    IReadOnlyList<FaceLampEmitterElement> Emitters);
