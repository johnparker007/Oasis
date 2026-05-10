# MAME Version Discovery Plan

This document defines the next discrete MAME workstream: replacing the hardcoded MAME version catalog with live latest-version discovery plus testable fallback behavior.

## Current Problem

The editor currently relies on a cached/static catalog similar to:

```json
{
  "KnownVersions": [
    "0281",
    "0267",
    "0258"
  ],
  "LatestVersion": "0281"
}
```

This means the editor cannot automatically discover new upstream MAME releases.

Avoid implementing discovery by probing sequential download URLs until a 404 is reached. That is fragile, slow, and unnecessarily noisy.

## Desired Design

Create live MAME version discovery with layered fallbacks:

1. Primary source: official MAME release page.
2. Secondary source: official MAME GitHub releases.
3. Tertiary source: cached catalog in LocalAppData.
4. Final fallback: compiled seed versions.

## Primary Source: mamedev.org Release Page

Fetch:

```text
https://www.mamedev.org/release.html
```

Parse the latest release from either of these patterns:

```regex
MAME\s+0\.(\d{3})\s+Official Binary Packages
```

or:

```regex
latest official MAME release is version\s+0\.(\d{3})
```

Normalize the result to Oasis/MAME download format:

```text
0.287 -> 0287
```

## Secondary Source: GitHub Releases

Fetch:

```text
https://github.com/mamedev/mame/releases
```

Parse from either release titles or tags:

```regex
MAME\s+0\.(\d{3})
```

or:

```regex
mame(\d{4})
```

Normalize to four digits.

## Cache Format

Upgrade the LocalAppData catalog JSON to include metadata:

```json
{
  "KnownVersions": ["0287", "0286", "0285", "0281", "0267", "0258"],
  "LatestVersion": "0287",
  "LastSuccessfulRefreshUtc": "2026-05-10T12:00:00Z",
  "Source": "MamedevReleasePage"
}
```

Suggested source values:

- `MamedevReleasePage`
- `GitHubReleases`
- `Cache`
- `SeedFallback`

## Service Design

Keep discovery separate from download/install.

Suggested structure:

```text
MameVersionCatalogService
    ├── TryFetchLatestFromMamedevReleasePageAsync
    ├── TryFetchLatestFromGitHubReleasesAsync
    ├── TryLoadCachedCatalogAsync
    ├── SaveCatalogCacheAsync
    └── GetSeedFallbackCatalog
```

Prefer adding small interfaces to make this testable:

```text
IMameReleasePageClient
IMameVersionCatalogCache
IMameClock
```

The release-page client can be backed by `HttpClient` in production and fake HTML strings in tests.

## Version Ordering

Do not sort version strings lexically unless they are guaranteed normalized to four digits.

Preferred:

- normalize all versions to four digits;
- parse as integer for ordering;
- de-duplicate;
- sort descending numeric.

Examples:

```text
287 -> 0287
0.287 -> 0287
mame0287 -> 0287
0287 -> 0287
```

## Known Versions Behavior

A live source may only expose the latest version, not all historical versions.

In that case:

- include the latest discovered version;
- merge with cached known versions;
- merge with seed fallback versions;
- de-duplicate;
- sort descending.

This preserves known older versions while still moving latest forward.

## Fallback Rules

Use this order:

1. Try mamedev.org.
2. If it fails or parse fails, try GitHub releases.
3. If it fails or parse fails, load cache.
4. If no cache exists, use seed fallback.

Important:

- network failure should not crash startup;
- parse failure should not crash startup;
- cache read/write failure should not crash startup;
- output log should record which source was used;
- Preferences should show whether the catalog is live or fallback.

## Tests

Yes, this can and should be tested.

Add parser/service tests that do not require live network access.

Recommended test project:

```text
WindowsNetProjects/OasisEditor/OasisEditor.Tests/
```

Use a lightweight .NET test framework such as xUnit or NUnit.

### Parser Tests

Test mamedev.org patterns:

- parses `MAME 0.287 Official Binary Packages` as `0287`;
- parses `The latest official MAME release is version 0.287` as `0287`;
- ignores unrelated version-looking text;
- returns no result for malformed HTML.

Test GitHub patterns:

- parses `MAME 0.287` as `0287`;
- parses tag/link text `mame0287` as `0287`;
- chooses highest version if multiple releases are present;
- returns no result for malformed HTML.

### Fallback Tests

Use fake clients/cache to verify:

- mamedev success skips GitHub/cache/seed;
- mamedev failure plus GitHub success uses GitHub;
- both network sources fail and cache exists uses cache;
- all live/cache sources fail uses seed fallback;
- cache write failure does not fail the returned live result;
- malformed cache falls back to seed;
- discovered latest merges with cached/seed known versions.

### Normalization Tests

Verify:

- `287` -> `0287`;
- `0.287` -> `0287`;
- `mame0287` -> `0287`;
- `0287` -> `0287`;
- invalid values are rejected or ignored.

### Ordering Tests

Verify:

- `0287`, `0281`, `0267`, `0258` sort descending;
- duplicates are removed;
- numeric ordering is used rather than string ordering.

## Suggested Implementation Steps for Codex

### Step 1 - Extract Parsing Logic

- Add pure parsing helpers for mamedev.org HTML and GitHub release HTML.
- Add normalization helper.
- Add unit tests for parsing and normalization.
- Do not change runtime behavior yet.

### Step 2 - Add Catalog Source Model

- Add catalog source enum/string values.
- Extend the cache model with `LastSuccessfulRefreshUtc` and `Source`.
- Keep backwards compatibility with the old cache JSON shape.
- Add tests for old/new cache JSON.

### Step 3 - Add Client Abstractions

- Add abstractions for fetching release page HTML.
- Inject or isolate HTTP access so tests use fake content.
- Keep production HTTP simple.

### Step 4 - Implement Fallback Chain

- Implement mamedev primary, GitHub secondary, cache tertiary, seed fallback.
- Add service tests for every fallback path.
- Log the selected source.

### Step 5 - Wire Into Existing Setup Flow

- Ensure background MAME setup uses the discovered latest version.
- Ensure Preferences shows latest version and source/fallback status.
- Ensure failure to refresh live catalog does not prevent using installed/cached MAME.

## Manual Verification

After implementation, John should verify:

- deleting the catalog cache still allows startup using seed fallback;
- normal online startup discovers latest MAME;
- the catalog JSON is rewritten with metadata;
- MAME auto-provisioning uses the discovered latest version;
- unplugged/offline startup uses cache or seed without crashing.
