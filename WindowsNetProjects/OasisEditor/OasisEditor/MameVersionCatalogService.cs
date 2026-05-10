using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace OasisEditor;

public sealed class MameVersionCatalogService : IMameVersionCatalogService
{
    private static readonly HttpClient HttpClient = new();
    private readonly MameDownloadService _downloadService;
    private readonly string _catalogCachePath;

    public MameVersionCatalogService(MameDownloadService downloadService)
    {
        _downloadService = downloadService;
        var cacheDirectory = Path.Combine(MameRuntimePaths.EnsureManagedRuntimeRootDirectory(), "state");
        Directory.CreateDirectory(cacheDirectory);
        _catalogCachePath = Path.Combine(cacheDirectory, "mame-version-catalog.json");
    }

    public async Task<MameVersionCatalogResult> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        var seed = MameVersionParsing.GetSeedVersions();
        var cached = await TryLoadCacheAsync(cancellationToken).ConfigureAwait(false);

        var discovered = await TryFetchLiveVersionAsync(cancellationToken).ConfigureAwait(false);
        if (discovered is not null)
        {
            var merged = MameVersionParsing.NormalizeSortAndDedupe([
                discovered.LatestVersion,
                .. discovered.KnownVersions,
                .. (cached?.KnownVersions ?? []),
                .. seed]);

            var latest = merged.FirstOrDefault();
            _ = TrySaveCacheAsync(new CatalogCacheModel(merged, latest, DateTime.UtcNow, discovered.Source), cancellationToken);
            return new MameVersionCatalogResult(latest, merged, false);
        }

        if (cached is not null)
        {
            var mergedCached = MameVersionParsing.NormalizeSortAndDedupe([.. (cached.KnownVersions ?? []), .. seed]);
            return new MameVersionCatalogResult(cached.LatestVersion ?? mergedCached.FirstOrDefault(), mergedCached, true);
        }

        var fallback = MameVersionParsing.NormalizeSortAndDedupe(seed);
        return new MameVersionCatalogResult(fallback.FirstOrDefault(), fallback, true);
    }

    public async Task<IReadOnlyList<string>> GetKnownVersionsAsync(CancellationToken cancellationToken)
    {
        var result = await GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
        return result.KnownVersions;
    }

    private async Task<LiveDiscoveryResult?> TryFetchLiveVersionAsync(CancellationToken cancellationToken)
    {
        var mamedevHtml = await TryGetStringAsync("https://www.mamedev.org/release.html", cancellationToken).ConfigureAwait(false);
        var mamedevVersion = mamedevHtml is null ? null : MameVersionParsing.TryParseLatestFromMamedevReleasePage(mamedevHtml);
        if (!string.IsNullOrWhiteSpace(mamedevVersion))
        {
            return new LiveDiscoveryResult(mamedevVersion, [mamedevVersion], "MamedevReleasePage");
        }

        var githubHtml = await TryGetStringAsync("https://github.com/mamedev/mame/releases", cancellationToken).ConfigureAwait(false);
        var githubVersion = githubHtml is null ? null : MameVersionParsing.TryParseLatestFromGitHubReleases(githubHtml);
        if (!string.IsNullOrWhiteSpace(githubVersion))
        {
            return new LiveDiscoveryResult(githubVersion, [githubVersion], "GitHubReleases");
        }

        return null;
    }

    private static async Task<string?> TryGetStringAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task TrySaveCacheAsync(CatalogCacheModel model, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_catalogCachePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cache write failures; live result remains valid.
        }
    }

    private async Task<CatalogCacheModel?> TryLoadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_catalogCachePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_catalogCachePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<CatalogCacheModel>(json);
        }
        catch
        {
            return null;
        }
    }

    private sealed record LiveDiscoveryResult(string LatestVersion, IReadOnlyList<string> KnownVersions, string Source);

    private sealed record CatalogCacheModel(
        IReadOnlyList<string> KnownVersions,
        string? LatestVersion,
        DateTime? LastSuccessfulRefreshUtc = null,
        string? Source = null);
}
