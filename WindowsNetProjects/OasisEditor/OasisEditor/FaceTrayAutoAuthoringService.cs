using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace OasisEditor;

internal sealed class FaceTrayAutoAuthoringService
{
    public FaceTrayAutoAuthoringResult AutoAuthor(FaceDocumentModel faceDocument, string? projectDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);

        var settings = (faceDocument.GenerationSettings ?? FaceGenerationSettingsModel.Default).Normalize();
        var trays = new List<FaceTrayModel>();
        var emitters = new List<FaceLampEmitterElement>();
        var nextTrayNumber = 1;

        foreach (var lampWindow in faceDocument.Elements
            .OfType<FaceLampWindowElement>()
            .Where(IsValidVisibleLampWindow)
            .OrderBy(CreateLampStableKey, StringComparer.Ordinal))
        {
            var contribution = FindBestContribution(lampWindow, faceDocument.MaskLayer);
            var baseBounds = contribution?.Bounds is { IsValid: true } contributionBounds
                ? contributionBounds
                : FaceSourceRegionModel.FromRect(new Rect(lampWindow.X, lampWindow.Y, lampWindow.Width, lampWindow.Height));
            var lampBounds = FaceSourceRegionModel.FromRect(new Rect(lampWindow.X, lampWindow.Y, lampWindow.Width, lampWindow.Height));
            var bounds = ExpandBounds(baseBounds, lampBounds, settings);
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

            var placement = ResolveEmitterPlacement(lampWindow, projectDirectory);

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
                CenterX = Math.Round(placement.CenterX, 2),
                CenterY = Math.Round(placement.CenterY, 2),
                IsAutoAuthored = true,
                AutoAuthoringSource = source,
                EmitterPlacementSource = placement.Source,
                Radius = placement.Radius is double radius ? Math.Round(radius, 2) : null,
                Diagnostics = placement.Diagnostics
            });
        }

        return new FaceTrayAutoAuthoringResult(DeriveTrayPolygons(trays), emitters);
    }

    public IReadOnlyList<FaceValidationDiagnostic> Validate(FaceDocumentModel faceDocument)
    {
        ArgumentNullException.ThrowIfNull(faceDocument);

        var diagnostics = new List<FaceValidationDiagnostic>();
        AddDuplicateDiagnostics(faceDocument.Trays.Select(tray => tray.ObjectId), "Face.Tray.DuplicateId", "tray", diagnostics);
        AddDuplicateDiagnostics(faceDocument.LampEmitters.Select(emitter => emitter.ObjectId), "Face.Emitter.DuplicateId", "emitter", diagnostics);
        AddDuplicateDiagnostics(
            faceDocument.LampEmitters.Where(emitter => emitter.TrayId > 0).Select(emitter => emitter.TrayId.ToString()),
            "Face.Tray.DuplicateNumericId",
            "numeric tray",
            diagnostics);

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

        foreach (var tray in faceDocument.Trays.Where(tray => !string.IsNullOrWhiteSpace(tray.ObjectId)))
        {
            if (!faceDocument.LampEmitters.Any(emitter => string.Equals(emitter.TrayObjectId?.Trim(), tray.ObjectId.Trim(), StringComparison.Ordinal)))
            {
                diagnostics.Add(new FaceValidationDiagnostic(
                    FaceValidationSeverity.Warning,
                    "Face.Tray.Emitter.Missing",
                    $"Face tray '{DisplayName(tray.ObjectId, tray.Name)}' does not have a lamp emitter."));
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


    private static EmitterPlacement ResolveEmitterPlacement(FaceLampWindowElement lampWindow, string? projectDirectory)
    {
        var fallbackSource = lampWindow.Width > 0d && lampWindow.Height > 0d
            ? "ComponentCentreFallback"
            : "LampWindowCentreFallback";
        var fallback = new EmitterPlacement(
            lampWindow.X + (lampWindow.Width / 2d),
            lampWindow.Y + (lampWindow.Height / 2d),
            fallbackSource,
            null,
            []);

        if (!lampWindow.SourceBlend)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(lampWindow.BulbMaskAssetPath))
        {
            return fallback with { Diagnostics = ["bulb-mask-missing", "centroid-fallback-used"] };
        }

        var centroid = FaceBulbMaskCentroidAnalyzer.AnalyzeFile(lampWindow.BulbMaskAssetPath, projectDirectory, out var diagnostic);
        if (centroid is null)
        {
            return fallback with { Diagnostics = string.IsNullOrWhiteSpace(diagnostic) ? ["centroid-fallback-used"] : [diagnostic, "centroid-fallback-used"] };
        }

        return new EmitterPlacement(
            lampWindow.X + (centroid.NormalizedX * lampWindow.Width),
            lampWindow.Y + (centroid.NormalizedY * lampWindow.Height),
            "MfmeBulbMaskCentroid",
            centroid.NormalizedRadius * Math.Max(lampWindow.Width, lampWindow.Height),
            []);
    }

    private static IReadOnlyList<FaceTrayModel> DeriveTrayPolygons(IReadOnlyList<FaceTrayModel> sourceTrays)
    {
        var working = sourceTrays
            .Select(tray => new TrayPolygonWorkItem(tray, tray.Bounds is { IsValid: true } bounds ? CreateRectangleVertices(bounds).ToList() : tray.Vertices.ToList(), []))
            .ToArray();

        for (var leftIndex = 0; leftIndex < working.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < working.Length; rightIndex++)
            {
                var left = working[leftIndex];
                var right = working[rightIndex];
                if (left.Tray.Bounds is not { IsValid: true } leftBounds || right.Tray.Bounds is not { IsValid: true } rightBounds)
                {
                    continue;
                }

                var leftRect = leftBounds.ToRect();
                var rightRect = rightBounds.ToRect();
                var overlap = Rect.Intersect(leftRect, rightRect);
                if (overlap.IsEmpty || overlap.Width <= 0d || overlap.Height <= 0d)
                {
                    continue;
                }

                var smallerArea = Math.Min(leftRect.Width * leftRect.Height, rightRect.Width * rightRect.Height);
                var largerArea = Math.Max(leftRect.Width * leftRect.Height, rightRect.Width * rightRect.Height);
                var overlapArea = overlap.Width * overlap.Height;
                if (smallerArea <= 0d)
                {
                    continue;
                }

                if (overlapArea / smallerArea >= 0.82d && largerArea / smallerArea >= 1.35d)
                {
                    left.Diagnostics.Add("contained-tray-candidate");
                    right.Diagnostics.Add("contained-tray-candidate");
                    continue;
                }

                if (overlapArea / smallerArea >= 0.65d)
                {
                    left.Diagnostics.Add("possible-shared-tray-candidate");
                    right.Diagnostics.Add("possible-shared-tray-candidate");
                    continue;
                }

                var leftCenter = Center(leftRect);
                var rightCenter = Center(rightRect);
                left.Vertices = ClipToNearestSide(left.Vertices, leftCenter, rightCenter);
                right.Vertices = ClipToNearestSide(right.Vertices, rightCenter, leftCenter);
                left.Diagnostics.Add("partial-overlap-clipped");
                right.Diagnostics.Add("partial-overlap-clipped");
            }
        }

        return working.Select(item =>
        {
            var vertices = item.Vertices.Count >= 3 ? RoundVertices(item.Vertices) : item.Tray.Vertices;
            if (item.Diagnostics.Count == 0 && string.Equals(item.Tray.AutoAuthoringSource, "lampWindowBounds", StringComparison.Ordinal) && IsRoundishIsolated(item.Tray, sourceTrays))
            {
                vertices = CreateOctagonVertices(item.Tray.Bounds!);
                item.Diagnostics.Add("isolated-roundish-octagon");
            }

            return new FaceTrayModel
            {
                ObjectId = item.Tray.ObjectId,
                Name = item.Tray.Name,
                IsAutoAuthored = item.Tray.IsAutoAuthored,
                AutoAuthoringSource = item.Tray.AutoAuthoringSource,
                SourceLampWindowObjectId = item.Tray.SourceLampWindowObjectId,
                SourcePanel2DElementId = item.Tray.SourcePanel2DElementId,
                LinkedMachineObjectReference = item.Tray.LinkedMachineObjectReference,
                Bounds = item.Tray.Bounds,
                Vertices = vertices,
                Diagnostics = item.Diagnostics.Distinct(StringComparer.Ordinal).OrderBy(diagnostic => diagnostic, StringComparer.Ordinal).ToArray()
            };
        }).ToArray();
    }

    private static List<FacePointModel> ClipToNearestSide(IReadOnlyList<FacePointModel> polygon, Point ownCenter, Point otherCenter)
    {
        if (polygon.Count < 3)
        {
            return polygon.ToList();
        }

        var midpoint = new Point((ownCenter.X + otherCenter.X) / 2d, (ownCenter.Y + otherCenter.Y) / 2d);
        var dx = otherCenter.X - ownCenter.X;
        var dy = otherCenter.Y - ownCenter.Y;
        var input = polygon.ToList();
        var output = new List<FacePointModel>();
        for (var index = 0; index < input.Count; index++)
        {
            var current = input[index];
            var previous = input[(index + input.Count - 1) % input.Count];
            var currentInside = IsOwnSide(current, midpoint, dx, dy);
            var previousInside = IsOwnSide(previous, midpoint, dx, dy);
            if (currentInside != previousInside)
            {
                output.Add(IntersectBisector(previous, current, midpoint, dx, dy));
            }

            if (currentInside)
            {
                output.Add(current);
            }
        }

        return output.Count >= 3 ? output : polygon.ToList();
    }

    private static bool IsOwnSide(FacePointModel point, Point midpoint, double dx, double dy)
    {
        return ((point.X - midpoint.X) * dx) + ((point.Y - midpoint.Y) * dy) <= 0.0001d;
    }

    private static FacePointModel IntersectBisector(FacePointModel start, FacePointModel end, Point midpoint, double dx, double dy)
    {
        var sx = start.X - midpoint.X;
        var sy = start.Y - midpoint.Y;
        var ex = end.X - start.X;
        var ey = end.Y - start.Y;
        var denominator = (ex * dx) + (ey * dy);
        var t = Math.Abs(denominator) <= double.Epsilon ? 0d : -(((sx * dx) + (sy * dy)) / denominator);
        t = Math.Clamp(t, 0d, 1d);
        return new FacePointModel { X = start.X + (ex * t), Y = start.Y + (ey * t) };
    }

    private static Point Center(Rect rect) => new(rect.X + (rect.Width / 2d), rect.Y + (rect.Height / 2d));

    private static IReadOnlyList<FacePointModel> RoundVertices(IReadOnlyList<FacePointModel> vertices)
    {
        return vertices.Select(vertex => new FacePointModel { X = Math.Round(vertex.X, 2), Y = Math.Round(vertex.Y, 2) }).ToArray();
    }

    private static bool IsRoundishIsolated(FaceTrayModel tray, IReadOnlyList<FaceTrayModel> trays)
    {
        if (tray.Bounds is not { IsValid: true } validBounds || validBounds.Width <= 0d || validBounds.Height <= 0d)
        {
            return false;
        }

        var aspect = validBounds.Width / validBounds.Height;
        if (aspect < 0.9d || aspect > 1.1d)
        {
            return false;
        }

        var rect = validBounds.ToRect();
        return !trays.Any(otherTray => HasPositiveIntersection(tray, otherTray, rect));
    }

    private static bool HasPositiveIntersection(FaceTrayModel tray, FaceTrayModel otherTray, Rect rect)
    {
        if (string.Equals(otherTray.ObjectId, tray.ObjectId, StringComparison.Ordinal) || otherTray.Bounds is not { IsValid: true } other)
        {
            return false;
        }

        var intersection = Rect.Intersect(rect, other.ToRect());
        return !intersection.IsEmpty && intersection.Width > 0d && intersection.Height > 0d;
    }

    private static IReadOnlyList<FacePointModel> CreateOctagonVertices(FaceSourceRegionModel bounds)
    {
        var left = bounds.X;
        var top = bounds.Y;
        var right = bounds.X + bounds.Width;
        var bottom = bounds.Y + bounds.Height;
        var insetX = bounds.Width * 0.2929d;
        var insetY = bounds.Height * 0.2929d;
        return RoundVertices([
            new FacePointModel { X = left + insetX, Y = top },
            new FacePointModel { X = right - insetX, Y = top },
            new FacePointModel { X = right, Y = top + insetY },
            new FacePointModel { X = right, Y = bottom - insetY },
            new FacePointModel { X = right - insetX, Y = bottom },
            new FacePointModel { X = left + insetX, Y = bottom },
            new FacePointModel { X = left, Y = bottom - insetY },
            new FacePointModel { X = left, Y = top + insetY }
        ]);
    }

    private sealed class TrayPolygonWorkItem
    {
        public TrayPolygonWorkItem(FaceTrayModel tray, List<FacePointModel> vertices, List<string> diagnostics)
        {
            Tray = tray;
            Vertices = vertices;
            Diagnostics = diagnostics;
        }

        public FaceTrayModel Tray { get; }
        public List<FacePointModel> Vertices { get; set; }
        public List<string> Diagnostics { get; }
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


    private static FaceSourceRegionModel ExpandBounds(FaceSourceRegionModel baseBounds, FaceSourceRegionModel lampWindowBounds, FaceGenerationSettingsModel settings)
    {
        var rect = baseBounds.ToRect();
        var padding = Math.Max(0d, settings.TrayBoundsPaddingPixels);
        if (padding > 0d)
        {
            rect.Inflate(padding, padding);
        }

        var inflationPercent = Math.Max(0d, settings.TrayBoundsInflationPercent);
        if (inflationPercent > 0d)
        {
            rect.Inflate(rect.Width * inflationPercent / 100d / 2d, rect.Height * inflationPercent / 100d / 2d);
        }

        if (settings.ClampTrayBoundsToLampWindow)
        {
            var clamped = Rect.Intersect(rect, lampWindowBounds.ToRect());
            rect = clamped.IsEmpty ? lampWindowBounds.ToRect() : clamped;
        }

        return FaceSourceRegionModel.FromRect(rect);
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

internal sealed record EmitterPlacement(double CenterX, double CenterY, string Source, double? Radius, IReadOnlyList<string> Diagnostics);
