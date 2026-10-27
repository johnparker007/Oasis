using OasisEditor.Features.LayoutImport;
using SkiaSharp;

namespace OasisEditor;

/// <summary>Optional updates for one native Face reel mount. Null values leave fields unchanged; Clear flags remove nullable fields.</summary>
public sealed record FaceReelMountUpdateOptions
{
    public string? Name { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public bool? Visible { get; init; }
    public bool? Locked { get; init; }
    public int? LogicalReelIndex { get; init; }
    public bool ClearLogicalReelReference { get; init; }
    public string? SymbolBandAssetPath { get; init; }
    public bool ClearSymbolBandAssetPath { get; init; }
    public int? Stops { get; init; }
    public bool ClearStops { get; init; }
    public double? VisibleScale { get; init; }
    public bool ClearVisibleScale { get; init; }
    public double? BandOffset { get; init; }
    public bool ClearBandOffset { get; init; }
    public bool? IsReversed { get; init; }
    public bool? ReelLampsEnabled { get; init; }
    public int? TopLampNumber { get; init; }
    public bool ClearTopLampNumber { get; init; }
    public int? MiddleLampNumber { get; init; }
    public bool ClearMiddleLampNumber { get; init; }
    public int? BottomLampNumber { get; init; }
    public bool ClearBottomLampNumber { get; init; }
    public bool? IsOpaqueReel { get; init; }
    public string? TransmissionMaskAssetPath { get; init; }
    public bool ClearTransmissionMaskAssetPath { get; init; }
}

public sealed partial class FaceAuthoringSession
{
    /// <summary>Adds a native reel mount with an explicitly authored logical reel reference.</summary>
    public string AddReelMount(string name, double x, double y, double width, double height, int reelIndex)
    {
        var document = RequireDocument();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Reel mount name cannot be blank.", nameof(name));
        if (reelIndex < 0) throw new ArgumentOutOfRangeException(nameof(reelIndex), "Logical reel index must be zero or greater.");
        ValidateGeometry(x, y, width, height);
        var mount = new FaceElementFile
        {
            ObjectId = $"face-reel-{Guid.NewGuid():N}",
            Name = name.Trim(),
            Kind = "reelMount",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsVisible = true,
            LinkedMachineObjectReference = MachineObjectReference.Reel(reelIndex).ToString(),
            ReelLampsEnabled = true,
            ReelLamps = DefaultReelLampSlots()
        };
        CommitReelDocument(document with { Elements = (document.Elements ?? []).Append(mount).ToArray() }, mount.ObjectId);
        return mount.ObjectId!;
    }

    /// <summary>Updates reel visual, lamp, logical-reference and placement fields as one Face history entry.</summary>
    public bool UpdateReelMount(string elementId, FaceReelMountUpdateOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(options);
        var document = RequireDocument();
        var elements = document.Elements?.ToArray() ?? [];
        var index = Array.FindIndex(elements, element => string.Equals(element.ObjectId, elementId, StringComparison.Ordinal));
        if (index < 0 || !string.Equals(elements[index].Kind, "reelMount", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException($"Face reel mount '{elementId}' was not found.");
        var current = elements[index];
        ValidateReelOptions(options);
        ValidateGeometry(options.X, options.Y, options.Width, options.Height);
        ValidateClearOptions(options);
        var name = options.Name is null ? current.Name : string.IsNullOrWhiteSpace(options.Name)
            ? throw new ArgumentException("Reel mount name cannot be blank.", nameof(options))
            : options.Name.Trim();
        var x = options.X ?? current.X;
        var y = options.Y ?? current.Y;
        var width = options.Width ?? current.Width;
        var height = options.Height ?? current.Height;
        ValidateUnlockedGeometry(current.LockTransform, current.X, current.Y, current.Width, current.Height, x, y, width, height);

        var currentProject = _project.RequireEditorProject();
        var bandPath = ApplyOptionalImagePath(current.AssetPath, options.SymbolBandAssetPath, options.ClearSymbolBandAssetPath, currentProject.ProjectDirectory);
        var maskPath = ApplyOptionalImagePath(current.ReelLampTransmissionMaskAssetPath, options.TransmissionMaskAssetPath, options.ClearTransmissionMaskAssetPath, currentProject.ProjectDirectory);

        var updated = current with
        {
            Name = name,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsVisible = options.Visible ?? current.IsVisible,
            LockTransform = options.Locked ?? current.LockTransform,
            LinkedMachineObjectReference = options.ClearLogicalReelReference ? null
                : options.LogicalReelIndex is int reelIndex ? MachineObjectReference.Reel(reelIndex).ToString() : current.LinkedMachineObjectReference,
            AssetPath = bandPath,
            Stops = options.ClearStops ? null : options.Stops ?? current.Stops,
            VisibleScale = options.ClearVisibleScale ? null : options.VisibleScale ?? current.VisibleScale,
            BandOffset = options.ClearBandOffset ? null : options.BandOffset ?? current.BandOffset,
            IsReversed = options.IsReversed ?? current.IsReversed,
            ReelLampsEnabled = options.ReelLampsEnabled ?? current.ReelLampsEnabled ?? true,
            ReelLamps = HasReelLampUpdates(options) ? UpdateReelLampNumbers(current.ReelLamps ?? [], options) : current.ReelLamps,
            IsOpaqueReel = options.IsOpaqueReel ?? current.IsOpaqueReel,
            ReelLampTransmissionMaskAssetPath = maskPath
        };
        if (updated == current) return false;
        elements[index] = updated;
        var nextDocument = document with { Elements = elements };
        if (FaceDocumentStorage.SerializePayload(nextDocument) == FaceDocumentStorage.SerializePayload(document)) return false;
        CommitReelDocument(nextDocument);
        return true;
    }

    /// <summary>Copies an actual image into the active Face package and assigns it as this mount's symbol band.</summary>
    public string ImportSymbolBand(string elementId, string sourceImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        var document = RequireDocument();
        var elements = document.Elements?.ToArray() ?? [];
        var index = Array.FindIndex(elements, element => string.Equals(element.ObjectId, elementId, StringComparison.Ordinal));
        if (index < 0 || !string.Equals(elements[index].Kind, "reelMount", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException($"Face reel mount '{elementId}' was not found.");
        var current = elements[index];
        var project = _project.RequireEditorProject();
        var source = ResolveImportSource(sourceImage);
        EnsurePackageUnchangedBeforeAssetImport(project.ProjectDirectory);
        var before = new Dictionary<string, string>(_savedPackageFiles, StringComparer.OrdinalIgnoreCase);
        var packageRelativePath = Path.GetRelativePath(project.ProjectDirectory, _packageDirectory
            ?? throw new InvalidOperationException("No Face package is open."));
        var folder = Path.Combine(packageRelativePath, "ReelBands");
        var imageName = _assetPaths.SanitizePathSegment(Path.GetFileNameWithoutExtension(source));
        var suffix = Guid.NewGuid().ToString("N");
        var imageRelative = Path.Combine(folder, $"{imageName}-{suffix}{Path.GetExtension(source)}");
        var imagePath = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, imageRelative);
        var createdPaths = new List<string>();
        try
        {
            using (var bitmap = SKBitmap.Decode(source) ?? throw new InvalidDataException("Symbol-band image could not be decoded.")) { }
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            WorkspacePathPolicy.RejectReparsePoints(project.ProjectDirectory, Path.GetRelativePath(project.ProjectDirectory, imagePath));
            File.Copy(source, imagePath, overwrite: false);
            createdPaths.Add(imagePath);
            var relativeImage = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, imagePath));
            ExtendPackageFingerprintAfterImport(project.ProjectDirectory, before, relativeImage);

            string? maskRelative = null;
            if (current.IsOpaqueReel)
            {
                var maskRelativePath = Path.Combine(folder, $"{imageName}-{suffix}_transmission.png");
                var maskPath = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, maskRelativePath);
                WorkspacePathPolicy.RejectReparsePoints(project.ProjectDirectory, Path.GetRelativePath(project.ProjectDirectory, maskPath));
                var maskDirectory = Path.GetDirectoryName(maskPath)!;
                Directory.CreateDirectory(maskDirectory);
                createdPaths.Add(maskPath);
                if (!MfmeReelLampTransmissionMaskGenerator.TryGenerate(imagePath, maskPath, out var error))
                    throw new InvalidDataException($"Could not generate the reel lamp transmission mask: {error}");
                maskRelative = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, maskPath));
                var beforeMask = new Dictionary<string, string>(_savedPackageFiles, StringComparer.OrdinalIgnoreCase);
                ExtendPackageFingerprintAfterImport(project.ProjectDirectory, beforeMask, maskRelative);
            }

            var updated = current with { AssetPath = relativeImage, ReelLampTransmissionMaskAssetPath = maskRelative };
            elements[index] = updated;
            CommitReelDocument(document with { Elements = elements });
            return relativeImage;
        }
        catch
        {
            foreach (var created in createdPaths)
                if (File.Exists(created)) File.Delete(created);
            _savedPackageFiles = before;
            throw;
        }
    }

    /// <summary>Generates and assigns a transmission mask from an existing reel symbol-band image.</summary>
    public string GenerateTransmissionMask(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        var document = RequireDocument();
        var elements = document.Elements?.ToArray() ?? [];
        var index = Array.FindIndex(elements, element => string.Equals(element.ObjectId, elementId, StringComparison.Ordinal));
        if (index < 0 || !string.Equals(elements[index].Kind, "reelMount", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException($"Face reel mount '{elementId}' was not found.");
        var current = elements[index];
        if (string.IsNullOrWhiteSpace(current.AssetPath))
            throw new InvalidOperationException("Assign a reel symbol-band image before generating its transmission mask.");

        var project = _project.RequireEditorProject();
        var bandAsset = ValidateExistingProjectImage(current.AssetPath, project.ProjectDirectory);
        EnsurePackageUnchangedBeforeAssetImport(project.ProjectDirectory);
        var before = new Dictionary<string, string>(_savedPackageFiles, StringComparer.OrdinalIgnoreCase);
        var packageRelativePath = Path.GetRelativePath(project.ProjectDirectory, _packageDirectory
            ?? throw new InvalidOperationException("No Face package is open."));
        var packageFolder = Path.Combine(packageRelativePath, "ReelBands");
        var stem = _assetPaths.SanitizePathSegment(Path.GetFileNameWithoutExtension(bandAsset));
        var maskRelativePath = Path.Combine(packageFolder, $"{stem}-{Guid.NewGuid():N}_transmission.png");
        var maskPath = WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, maskRelativePath);
        var created = false;
        try
        {
            WorkspacePathPolicy.RejectReparsePoints(project.ProjectDirectory, Path.GetRelativePath(project.ProjectDirectory, maskPath));
            if (File.Exists(maskPath) || Directory.Exists(maskPath)) throw new IOException("A reel transmission mask already exists at the reserved destination.");
            Directory.CreateDirectory(Path.GetDirectoryName(maskPath)!);
            created = true;
            if (!MfmeReelLampTransmissionMaskGenerator.TryGenerate(
                    WorkspacePathPolicy.ResolveConfined(project.ProjectDirectory, bandAsset, requireExists: true), maskPath, out var error))
                throw new InvalidDataException($"Could not generate the reel lamp transmission mask: {error}");
            var relativeMask = ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, maskPath));
            ExtendPackageFingerprintAfterImport(project.ProjectDirectory, before, relativeMask);

            elements[index] = current with { ReelLampTransmissionMaskAssetPath = relativeMask };
            var nextDocument = document with { Elements = elements };
            if (FaceDocumentStorage.SerializePayload(nextDocument) == FaceDocumentStorage.SerializePayload(document))
                return relativeMask;
            CommitReelDocument(nextDocument);
            return relativeMask;
        }
        catch
        {
            if (created && File.Exists(maskPath)) File.Delete(maskPath);
            _savedPackageFiles = before;
            throw;
        }
    }

    private string? ApplyOptionalImagePath(string? current, string? requested, bool clear, string projectRoot)
    {
        if (requested is not null) return ValidateExistingProjectImage(requested, projectRoot);
        return clear ? null : current;
    }

    private void CommitReelDocument(FaceDocumentFile document, string? selectedId = null)
    {
        var serialized = FaceDocumentStorage.Serialize(document);
        if (!FaceDocumentStorage.TryReadValidated(serialized, out _, out var error))
            throw new InvalidDataException(error);
        Commit(document, selectedId);
    }

    private static string ValidateExistingProjectImage(string path, string projectRoot)
    {
        var normalized = AssetReference.Normalize(path);
        var resolved = WorkspacePathPolicy.ResolveConfined(projectRoot, normalized, requireExists: true);
        WorkspacePathPolicy.RejectReparsePoints(projectRoot, normalized, requireExists: true);
        if (!File.Exists(resolved)) throw new FileNotFoundException("Face reel artwork image was not found.", resolved);
        using var bitmap = SKBitmap.Decode(resolved) ?? throw new InvalidDataException("Face reel artwork must reference a decodable image.");
        return normalized;
    }

    private static FaceReelLampFile[] UpdateReelLampNumbers(IReadOnlyList<FaceReelLampFile> lamps, FaceReelMountUpdateOptions options)
    {
        var slots = DefaultReelLampSlots().ToDictionary(lamp => lamp.Position!, StringComparer.OrdinalIgnoreCase);
        foreach (var lamp in lamps)
            if (lamp.Position is not null && slots.ContainsKey(lamp.Position)) slots[lamp.Position] = lamp;
        Set("top", options.TopLampNumber, options.ClearTopLampNumber);
        Set("middle", options.MiddleLampNumber, options.ClearMiddleLampNumber);
        Set("bottom", options.BottomLampNumber, options.ClearBottomLampNumber);
        return [slots["top"], slots["middle"], slots["bottom"]];

        void Set(string position, int? number, bool clear)
        {
            if (number is null && !clear) return;
            slots[position] = slots[position] with { LampNumber = clear ? null : number };
        }
    }

    private static FaceReelLampFile[] DefaultReelLampSlots() =>
    [
        new() { Position = "top", LocalVerticalCenter = 1d / 6d, Radius = 0d, Intensity = 1d },
        new() { Position = "middle", LocalVerticalCenter = 0.5d, Radius = 0d, Intensity = 1d },
        new() { Position = "bottom", LocalVerticalCenter = 5d / 6d, Radius = 0d, Intensity = 1d }
    ];

    private static bool HasReelLampUpdates(FaceReelMountUpdateOptions options)
        => options.TopLampNumber.HasValue || options.ClearTopLampNumber || options.MiddleLampNumber.HasValue
            || options.ClearMiddleLampNumber || options.BottomLampNumber.HasValue || options.ClearBottomLampNumber;

    private static void ValidateReelOptions(FaceReelMountUpdateOptions options)
    {
        if (options.LogicalReelIndex is < 0) throw new ArgumentOutOfRangeException(nameof(options.LogicalReelIndex), "Logical reel index must be zero or greater.");
        if (options.Stops is < 0) throw new ArgumentOutOfRangeException(nameof(options.Stops), "Reel stops must be zero or greater.");
        if (options.VisibleScale is double scale && (!double.IsFinite(scale) || scale <= 0)) throw new ArgumentOutOfRangeException(nameof(options.VisibleScale), "Visible scale must be positive and finite.");
        if (options.BandOffset is double offset && (!double.IsFinite(offset) || offset is < -1d or > 1d)) throw new ArgumentOutOfRangeException(nameof(options.BandOffset), "Band offset must be finite and from -1 to 1.");
        foreach (var number in new[] { options.TopLampNumber, options.MiddleLampNumber, options.BottomLampNumber })
            if (number is < 0) throw new ArgumentOutOfRangeException(nameof(options), "Reel lamp numbers must be zero or greater.");
    }

    private static void ValidateClearOptions(FaceReelMountUpdateOptions options)
    {
        if (options.ClearLogicalReelReference && options.LogicalReelIndex.HasValue
            || options.ClearSymbolBandAssetPath && options.SymbolBandAssetPath is not null
            || options.ClearStops && options.Stops.HasValue
            || options.ClearVisibleScale && options.VisibleScale.HasValue
            || options.ClearBandOffset && options.BandOffset.HasValue
            || options.ClearTopLampNumber && options.TopLampNumber.HasValue
            || options.ClearMiddleLampNumber && options.MiddleLampNumber.HasValue
            || options.ClearBottomLampNumber && options.BottomLampNumber.HasValue
            || options.ClearTransmissionMaskAssetPath && options.TransmissionMaskAssetPath is not null)
            throw new ArgumentException("A field cannot be both updated and cleared in the same reel mount edit.", nameof(options));
    }
}
