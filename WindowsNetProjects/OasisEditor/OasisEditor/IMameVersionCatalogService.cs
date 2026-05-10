namespace OasisEditor;

public interface IMameVersionCatalogService
{
    Task<MameVersionCatalogResult> GetLatestVersionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetKnownVersionsAsync(CancellationToken cancellationToken);
}

public sealed record MameVersionCatalogResult(
    string? LatestVersion,
    IReadOnlyList<string> KnownVersions,
    bool IsFromCache);
