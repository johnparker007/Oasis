using System.Text.Json;
using System.Text.Json.Serialization;

namespace OasisEditor;

/// <summary>A reusable authored physical reel definition.</summary>
public sealed record ReelDocument(int Version, string Id, string DisplayName, double DiameterMm, double WidthMm)
{
    public const int CurrentVersion = 1;

    public static ReelDocument Create(string displayName) => new(
        CurrentVersion, Guid.NewGuid().ToString("D"), displayName.Trim(), 200, 70);
}

public static class ReelDocumentStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Serialize(ReelDocument document)
    {
        Validate(document);
        return JsonSerializer.Serialize(document with { Version = ReelDocument.CurrentVersion }, Options);
    }

    public static bool TryRead(string? json, out ReelDocument document, out string error)
    {
        document = ReelDocument.Create("Reel");
        error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Reel document is empty.");
            document = JsonSerializer.Deserialize<ReelDocument>(json, Options)
                ?? throw new InvalidOperationException("Reel document is empty.");
            Validate(document);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            error = $"Invalid Reel document: {exception.Message}";
            return false;
        }
    }

    public static void Validate(ReelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Version != ReelDocument.CurrentVersion) throw new InvalidOperationException($"Unsupported Reel schema version. This editor supports only version {ReelDocument.CurrentVersion}.");
        if (!Guid.TryParse(document.Id, out _)) throw new InvalidOperationException("Reel ID must be a stable GUID.");
        if (string.IsNullOrWhiteSpace(document.DisplayName)) throw new InvalidOperationException("Reel display name is required.");
        if (!PanelElementValidation.IsFinite(document.DiameterMm) || document.DiameterMm <= 0) throw new InvalidOperationException("Reel diameter must be a positive finite millimetre value.");
        if (!PanelElementValidation.IsFinite(document.WidthMm) || document.WidthMm <= 0) throw new InvalidOperationException("Reel width must be a positive finite millimetre value.");
    }
}
