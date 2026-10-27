using System.Text.Json;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

/// <summary>Portable edits for a Cabinet3D package using the existing Cabinet document contract.</summary>
public sealed class CabinetAuthoringSession : ThreeDAuthoringSessionBase
{
    private string _payload = string.Empty;
    private CabinetDocument _document = CabinetDocument.Empty;
    private static readonly JsonSerializerOptions CloneOptions = new() { PropertyNameCaseInsensitive = true };

    public CabinetAuthoringSession(AuthoringSession projectSession)
        : base(projectSession, "Cabinet3D", ProjectAssetPathService.Cabinet3DManifestFileName) { }

    public CabinetDocument Document => Clone(_document);
    public event Action? Changed;
    protected override string CurrentPayload => _payload;

    public void Open(string manifestPath)
    {
        var manifest = ResolveManifest(manifestPath);
        var json = File.ReadAllText(manifest);
        if (!CabinetDocumentStorage.TryRead(json, out var document)) throw new InvalidDataException("Invalid Cabinet3D manifest.");
        _document = Clone(document);
        _payload = CabinetDocumentStorage.Serialize(_document);
        SetOpen(manifest, _payload);
        ValidatePackage();
        Changed?.Invoke();
    }

    public string Create(string name, string sourceProjectRelativePath)
    {
        _ = ResolveSource(sourceProjectRelativePath);
        var package = CreatePackage(name);
        PackageDirectoryForCreate(package);
        var modelPath = CopyModel(sourceProjectRelativePath);
        _document = CabinetDocument.FromModelPath(modelPath);
        _payload = CabinetDocumentStorage.Serialize(_document);
        var manifest = Path.Combine(package, ProjectAssetPathService.Cabinet3DManifestFileName);
        WriteNewFile(manifest, _payload);
        MarkCreated(manifest, _payload);
        Changed?.Invoke();
        return manifest;
    }

    public string ImportModel(string sourceProjectRelativePath)
    {
        EnsurePackageUnchanged();
        var copiedPath = CopyModel(sourceProjectRelativePath);
        var updated = _document with { Model = _document.Model with { Path = copiedPath } };
        Commit(updated);
        return copiedPath;
    }

    public bool SetModel(double? scale = null, string? upAxis = null)
    {
        var current = _document.Model;
        var model = current with { Scale = scale ?? current.Scale, UpAxis = upAxis ?? current.UpAxis };
        if (!double.IsFinite(model.Scale) || model.Scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale), "Model scale must be positive and finite.");
        if (model.UpAxis is not ("X" or "Y" or "Z")) throw new ArgumentException("Model up-axis must be X, Y, or Z.", nameof(upAxis));
        return Commit(_document with { Model = model });
    }

    public bool SetSurfaceTargetSettings(IEnumerable<CabinetSurfaceTargetSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Select(s => s?.Normalized() ?? throw new ArgumentException("Surface-target settings cannot contain null entries.", nameof(settings))).ToArray();
        if (normalized.Any(s => string.IsNullOrWhiteSpace(s.TargetId)) || normalized.Select(s => s.TargetId).Distinct(StringComparer.Ordinal).Count() != normalized.Length)
            throw new ArgumentException("Surface-target IDs must be non-empty and unique.", nameof(settings));
        return Commit(_document with { SurfaceTargetSettings = normalized });
    }

    public bool SetSurfaceTarget(CabinetSurfaceTargetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.TargetId)) throw new ArgumentException("Target ID is required.", nameof(settings));
        var existing = (_document.SurfaceTargetSettings ?? []).Where(s => !string.Equals(s.TargetId, settings.TargetId, StringComparison.Ordinal));
        return SetSurfaceTargetSettings(existing.Append(settings));
    }

    public bool SetReflections(IEnumerable<CabinetReflectionDefinition> reflections)
    {
        ArgumentNullException.ThrowIfNull(reflections);
        var items = reflections.Select(r => r?.Normalized() ?? throw new ArgumentException("Reflection definitions cannot contain null entries.", nameof(reflections))).ToArray();
        ValidateReflections(items);
        return Commit(_document with { Reflections = items });
    }

    public void Save()
    {
        ValidatePackage();
        _payload = CabinetDocumentStorage.Serialize(_document);
        SavePayload(_payload);
        Changed?.Invoke();
    }

    protected override void SetCurrentPayload(string payload)
    {
        if (!CabinetDocumentStorage.TryRead(payload, out var document)) throw new InvalidDataException("Invalid Cabinet3D history entry.");
        _document = Clone(document);
        _payload = payload;
        Changed?.Invoke();
    }

    private bool Commit(CabinetDocument candidate)
    {
        EnsureEditingAsset();
        Validate(candidate);
        var payload = CabinetDocumentStorage.Serialize(candidate);
        if (payload == _payload) return false;
        CommitPayload(payload); return true;
    }

    private void ValidatePackage()
    {
        Validate(_document);
        if (PackageDirectory is not null)
        {
            var modelPath = WorkspacePathPolicy.ResolveConfined(PackageDirectory, _document.Model.Path, requireExists: true);
            WorkspacePathPolicy.RejectReparsePoints(PackageDirectory, _document.Model.Path, requireExists: true);
            if (!File.Exists(modelPath) || !Path.GetExtension(modelPath).Equals(".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Cabinet model must point to an existing package GLB.");
        }
    }

    private static void Validate(CabinetDocument document)
    {
        if (document.Version != 9 || document.Model is null || !CabinetDocumentStorage.IsSafePackageRelativePath(document.Model.Path)) throw new InvalidDataException("Cabinet3D document has an unsupported version or model path.");
        if (!double.IsFinite(document.Model.Scale) || document.Model.Scale <= 0) throw new InvalidDataException("Cabinet model scale must be positive and finite.");
        if (document.Model.UpAxis is not ("X" or "Y" or "Z")) throw new InvalidDataException("Cabinet model up-axis must be X, Y, or Z.");
        var targets = document.SurfaceTargetSettings ?? [];
        if (targets.Any(s => s is null || string.IsNullOrWhiteSpace(s.TargetId)) || targets.Select(s => s.TargetId).Distinct(StringComparer.Ordinal).Count() != targets.Length)
            throw new InvalidDataException("Surface-target settings require unique, non-empty target IDs.");
        ValidateReflections(document.Reflections ?? []);
    }

    private static void ValidateReflections(IReadOnlyList<CabinetReflectionDefinition> reflections)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reflection in reflections)
        {
            if (reflection is null || string.IsNullOrWhiteSpace(reflection.Id) || !ids.Add(reflection.Id) || string.IsNullOrWhiteSpace(reflection.TargetId)
                || reflection.MaterialSlot < 0 || reflection.Sources is null || reflection.Sources.Length > CabinetReflectionContract.MaximumSources || reflection.Settings is null)
                throw new InvalidDataException("Reflection definitions require unique IDs, a target, a valid material slot, settings, and at most four sources.");
            var settings = reflection.Settings;
            var numericSettings = new[] { settings.Strength, settings.UnlitArtworkStrength, settings.LitLampStrength, settings.FresnelPower, settings.FresnelStrength, settings.Roughness, settings.Distortion, settings.EdgeFade };
            if (numericSettings.Any(value => !double.IsFinite(value)) || settings.Strength is < 0 or > 2 || settings.UnlitArtworkStrength is < 0 or > 2
                || settings.LitLampStrength is < 0 or > 4 || settings.FresnelPower is < .1 or > 10 || settings.FresnelStrength is < 0 or > 2
                || settings.Roughness is < 0 or > 1 || settings.Distortion is < 0 or > .05 || settings.EdgeFade is < 0 or > .25)
                throw new InvalidDataException($"Reflection '{reflection.Id}' contains settings outside the supported finite ranges.");
            foreach (var source in reflection.Sources)
                if (source is null || string.IsNullOrWhiteSpace(source.SourceSurfaceTargetId) || source.Plane is null
                    || !CabinetReflectionPlaneValidation.TryValidate(source.Plane, out _)
                    || (source.Plane.Normal is { } normal && (!double.IsFinite(normal.X) || !double.IsFinite(normal.Y) || !double.IsFinite(normal.Z)))
                    || source.PlaneSource is not (CabinetReflectionPlaneSource.Automatic or CabinetReflectionPlaneSource.Manual))
                    throw new InvalidDataException($"Reflection '{reflection.Id}' contains an invalid source definition.");
        }
    }

    private static CabinetDocument Clone(CabinetDocument document) => JsonSerializer.Deserialize<CabinetDocument>(CabinetDocumentStorage.Serialize(document), CloneOptions)!;
    private void PackageDirectoryForCreate(string package) => SetPackageForCreate(package);
    private void MarkCreated(string manifest, string payload) => SetCreated(manifest, payload);
}
