using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public enum Object3DColliderKind { None, Sphere, Box, Capsule, Mesh }
public enum Object3DCapsuleAxis { X, Y, Z }

public sealed record Object3DModelDefinition(string Path, double Scale, string UpAxis);
public sealed record Object3DColliderDefinition(
    Object3DColliderKind Kind,
    double[]? Center = null,
    double? Radius = null,
    double[]? Size = null,
    double? Height = null,
    Object3DCapsuleAxis? Axis = null)
{
    public static Object3DColliderDefinition None => new(Object3DColliderKind.None);
}
public sealed record Object3DRigidbodyDefinition(bool Enabled, double? Mass = null, bool? UseGravity = null);
public sealed record Object3DPhysicsDefinition(Object3DColliderDefinition Collider, Object3DRigidbodyDefinition Rigidbody);

/// <summary>A reusable authored physical 3D object definition.</summary>
public sealed record Object3DDocument(
    int SchemaVersion,
    string Id,
    string DisplayName,
    Object3DModelDefinition Model,
    Object3DPhysicsDefinition Physics)
{
    public const int CurrentSchemaVersion = 1;

    public static Object3DDocument Create(string displayName) => new(
        CurrentSchemaVersion,
        Guid.NewGuid().ToString("D"),
        displayName.Trim(),
        new Object3DModelDefinition("model.glb", 1, "Y"),
        new Object3DPhysicsDefinition(Object3DColliderDefinition.None, new Object3DRigidbodyDefinition(false)));
}

public static class Object3DValidationService
{
    public static void Validate(Object3DDocument document, string? packageDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != Object3DDocument.CurrentSchemaVersion) throw new InvalidOperationException($"Unsupported Object3D schema version. This editor supports only version {Object3DDocument.CurrentSchemaVersion}.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Object3D ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException("Object3D display name is required.");
        if (document.Model is null) throw new InvalidOperationException("Object3D model settings are required.");
        if (!CabinetDocumentStorage.IsSafePackageRelativePath(document.Model.Path)) throw new InvalidOperationException("Object3D model path must be a safe package-relative path.");
        if (!string.Equals(Path.GetExtension(document.Model.Path), ".glb", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Object3D model path must identify a GLB file.");
        if (!FinitePositive(document.Model.Scale)) throw new InvalidOperationException("Object3D model scale must be positive and finite.");
        if (document.Model.UpAxis is not ("X" or "Y" or "Z")) throw new InvalidOperationException("Object3D model up-axis must be X, Y, or Z.");
        if (document.Physics?.Collider is null || document.Physics.Rigidbody is null) throw new InvalidOperationException("Object3D physics settings are required.");

        var collider = document.Physics.Collider;
        if (!Enum.IsDefined(collider.Kind)) throw new InvalidOperationException("Object3D collider kind is unsupported.");
        if (collider.Kind is Object3DColliderKind.Sphere or Object3DColliderKind.Box or Object3DColliderKind.Capsule) ValidateVector(collider.Center, "collider center");
        if (collider.Kind == Object3DColliderKind.Sphere && !FinitePositive(collider.Radius)) throw new InvalidOperationException("Sphere collider radius must be positive and finite.");
        if (collider.Kind == Object3DColliderKind.Box)
        {
            ValidateVector(collider.Size, "Box collider size");
            if (collider.Size!.Any(value => value <= 0)) throw new InvalidOperationException("Box collider dimensions must be positive.");
        }
        if (collider.Kind == Object3DColliderKind.Capsule)
        {
            if (!FinitePositive(collider.Radius) || !FinitePositive(collider.Height)) throw new InvalidOperationException("Capsule collider radius and height must be positive and finite.");
            if (collider.Height < collider.Radius * 2) throw new InvalidOperationException("Capsule collider height must be at least twice its radius.");
            if (collider.Axis is null || !Enum.IsDefined(collider.Axis.Value)) throw new InvalidOperationException("Capsule collider axis must be X, Y, or Z.");
        }
        if (document.Physics.Rigidbody.Enabled && !FinitePositive(document.Physics.Rigidbody.Mass)) throw new InvalidOperationException("Enabled Rigidbody mass must be positive and finite.");

        if (packageDirectory is not null)
        {
            var root = Path.GetFullPath(packageDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var modelPath = Path.GetFullPath(Path.Combine(root, document.Model.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!modelPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(modelPath))
                throw new InvalidOperationException("Object3D model is missing or outside its package.");
        }
    }

    private static bool FinitePositive(double? value) => value is > 0 && double.IsFinite(value.Value);
    private static void ValidateVector(double[]? value, string name)
    {
        if (value is null || value.Length != 3 || value.Any(component => !double.IsFinite(component))) throw new InvalidOperationException($"Object3D {name} must contain three finite values.");
    }
}

public static class Object3DDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(Object3DDocument document)
    {
        Object3DValidationService.Validate(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static bool TryRead(string? json, out Object3DDocument document, out string error)
    {
        document = Object3DDocument.Create("Object3D");
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Object3D document is empty.");
            document = JsonSerializer.Deserialize<Object3DDocument>(json, Options) ?? throw new InvalidOperationException("Object3D document is empty.");
            Object3DValidationService.Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            error = $"Invalid Object3D document: {exception.Message}";
            return false;
        }
    }
}
