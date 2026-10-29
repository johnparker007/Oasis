using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OasisEditor;

public sealed record UnreferencedAsset(string Path, long Bytes, string Fingerprint, bool IsDirectory = false);
public sealed record AssetCleanupPlan(string Id, string ProjectFile, bool FmlOnly,
    IReadOnlyList<UnreferencedAsset> Candidates, ProjectCleanupLease? Owner, IReadOnlyList<string>? ProtectedRoots = null);
public sealed record AssetCleanupResult(IReadOnlyList<string> Deleted, IReadOnlyList<string> Failures);

/// <summary>Conservative cleanup of loose media, never authored packages, scripts, ROMs or Library files.</summary>
public static class UnreferencedAssetService
{
    private static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly HashSet<string> Media = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif", ".tif", ".tiff", ".svg", ".wav", ".ogg", ".mp3", ".flac", ".glb", ".gltf" };
    private static readonly HashSet<string> Text = new(StringComparer.OrdinalIgnoreCase)
    { ".json", ".gltf", ".oasis", ".cs", ".lua", ".obj", ".mtl", ".txt", ".xml", ".ini", ".cfg", ".yaml", ".yml", ".gam", ".css", ".html", ".oasisproj" };
    private static readonly Regex Quoted = new("[\"']([^\"'\\r\\n]+)[\"']", RegexOptions.Compiled);
    private static readonly Regex Urls = new("url\\(\\s*['\"]?([^)'\"\\r\\n]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const int MaximumMetadataBytes = 32 * 1024 * 1024;
    private static bool Manifest(string file) => Path.GetFileName(file).StartsWith("asset.", StringComparison.OrdinalIgnoreCase) && !Media.Contains(Path.GetExtension(file));

    public static AssetCleanupPlan Scan(string projectFile, IEnumerable<AssetReferenceSnapshot> history,
        bool fmlOnly = false, ProjectCleanupLease? owner = null, IEnumerable<string>? protectedRoots = null)
    {
        projectFile = Path.GetFullPath(projectFile);
        var root = Path.GetDirectoryName(projectFile)!;
        WorkspacePathPolicy.ResolveConfined(root, Path.GetFileName(projectFile), requireExists: true);
        ProjectCleanupLease.EnsureCanClean(projectFile, owner);
        var assets = WorkspacePathPolicy.ResolveConfined(root, "Assets", requireExists: true);
        var excluded = (protectedRoots ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.GetFullPath(p, root)).Distinct(Paths).ToArray();
        var files = Files(root, excluded).ToArray();
        var importing = files.Where(f => Path.GetFileName(f) == ".oasis-importing.lock" && IsLiveImport(f))
            .Select(f => Path.GetDirectoryName(f)!).ToArray();
        var allFiles = files.ToHashSet(Paths);
        var packages = files.Where(f => Manifest(f) && Within(assets, f))
            .Select(f => Path.GetDirectoryName(f)!).ToArray();
        var referenced = new HashSet<string>(Paths); var referencedFolders = new HashSet<string>(Paths);

        void Mark(string value, string directory)
        {
            value = value.Trim();
            if (value.Length == 0 || value.Length > 4096 || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile && uri.Scheme is "http" or "https") return;
            if (uri?.IsFile == true) value = uri.LocalPath;
            foreach (var candidate in new[] { value, Uri.UnescapeDataString(value).Split('#')[0] }.Distinct())
            foreach (var basis in new[] { directory, root, assets })
            {
                try
                {
                    var full = Path.GetFullPath(candidate.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar), basis);
                    if (allFiles.Contains(full)) referenced.Add(full);
                    // Treat potential directory references conservatively, without
                    // probing untrusted paths (including external file:// URIs).
                    if (!Paths.Equals(full, assets) && !Paths.Equals(full, root) && Within(assets, full)) referencedFolders.Add(full);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
            }
        }
        void Content(string content, string directory, bool json)
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                Visit(document.RootElement); return;
            }
            catch (JsonException) when (!json) { }
            foreach (Match match in Quoted.Matches(content)) Mark(match.Groups[1].Value, directory);
            foreach (Match match in Urls.Matches(content)) Mark(match.Groups[1].Value, directory);
            foreach (var line in content.Split('\n'))
            {
                Mark(line, directory);
                var separator = line.IndexOfAny([' ', '=', ':']);
                if (separator >= 0) Mark(line[(separator + 1)..], directory);
                if (Media.Any(extension => line.Contains(extension, StringComparison.OrdinalIgnoreCase)))
                    foreach (var form in new[] { line, line.Split('#')[0] })
                        for (var start = 0; start < form.Length; start++)
                            if (start == 0 || char.IsWhiteSpace(form[start - 1])) Mark(form[start..], directory);
            }
            void Visit(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        var text = element.GetString()!; Mark(text, directory);
                        foreach (Match match in Quoted.Matches(text)) Mark(match.Groups[1].Value, directory);
                        break;
                    case JsonValueKind.Array: foreach (var child in element.EnumerateArray()) Visit(child); break;
                    case JsonValueKind.Object:
                        foreach (var property in element.EnumerateObject()) { Mark(property.Name, directory); Visit(property.Value); }
                        break;
                }
            }
        }
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file); var folder = Path.GetDirectoryName(file)!;
            if (extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(file); using var reader = new BinaryReader(stream);
                if (reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2) throw new InvalidDataException($"Cannot safely scan GLB dependencies: {file}");
                _ = reader.ReadUInt32(); var length = reader.ReadUInt32(); var type = reader.ReadUInt32();
                if (type != 0x4E4F534A || length > MaximumMetadataBytes) throw new InvalidDataException($"Cannot safely scan GLB dependencies: {file}");
                var bytes = reader.ReadBytes(checked((int)length));
                if (bytes.Length != length) throw new EndOfStreamException($"Incomplete GLB metadata: {file}");
                Content(Encoding.UTF8.GetString(bytes), folder, true);
            }
            else if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumMetadataBytes });
                var xml = XDocument.Load(reader);
                foreach (var attribute in xml.Descendants().Attributes())
                {
                    Mark(attribute.Value, folder);
                    foreach (Match match in Urls.Matches(attribute.Value)) Mark(match.Groups[1].Value, folder);
                }
            }
            else if (Text.Contains(extension) || Manifest(file))
            {
                if (new FileInfo(file).Length > MaximumMetadataBytes) throw new InvalidDataException($"Reference metadata exceeds the cleanup scan limit: {file}");
                var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(file)).TrimStart('\uFEFF');
                Content(text, folder, extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".oasisproj", StringComparison.OrdinalIgnoreCase)
                    || Manifest(file));
            }
        }
        foreach (var snapshot in history) Content(snapshot.Content, snapshot.BaseDirectory, false);
        var fmlRoot = Path.Combine(assets, "FmlImport");
        // An import is one source bundle. A live reference to any image keeps
        // its companion masks/originals, including files not directly rendered.
        var liveFmlBundles = referenced.Concat(referencedFolders).Where(f => Within(fmlRoot, f))
            .Select(f => Path.GetRelativePath(fmlRoot, f).Split(Path.DirectorySeparatorChar)[0])
            .Select(name => Path.Combine(fmlRoot, name)).Distinct(Paths).ToArray();
        bool Protected(string file) => referenced.Contains(file) || referencedFolders.Any(d => Within(d, file))
            || importing.Any(d => Within(d, file)) || liveFmlBundles.Any(d => Within(d, file));
        var candidates = new List<UnreferencedAsset>();
        foreach (var file in files.Where(f => Within(assets, f) && (!fmlOnly || Within(fmlRoot, f))))
        {
            var staleImportLock = Path.GetFileName(file) == ".oasis-importing.lock" && !importing.Any(d => Within(d, file));
            if ((!Media.Contains(Path.GetExtension(file)) && !staleImportLock) || packages.Any(p => Within(p, file)) || Protected(file)) continue;
            candidates.Add(new(Relative(root, file), new FileInfo(file).Length, Hash(file)));
        }
        if (Directory.Exists(fmlRoot))
            foreach (var folder in Directory.EnumerateDirectories(fmlRoot))
                if (!Path.GetFileName(folder).StartsWith(".oasis-", StringComparison.OrdinalIgnoreCase)
                    && !excluded.Any(d => Within(folder, d) || Within(d, folder))
                    && !files.Any(f => Within(folder, f)) && !referencedFolders.Any(f => Within(f, folder) || Within(folder, f))
                    && !packages.Any(p => Within(folder, p)))
                    candidates.Add(new(Relative(root, folder), 0, EmptyTreeHash(folder), true));
        return new(Guid.NewGuid().ToString("N"), projectFile, fmlOnly, candidates.OrderBy(c => c.Path, StringComparer.Ordinal).ToArray(), owner, excluded);
    }

    public static AssetCleanupResult DeleteSelected(AssetCleanupPlan plan, IEnumerable<string> selected,
        IEnumerable<AssetReferenceSnapshot> currentHistory, IEnumerable<string>? additionalProtectedRoots = null)
    {
        var original = plan.Candidates.ToDictionary(c => c.Path, Paths);
        var paths = selected.Distinct(Paths).ToArray();
        if (paths.Any(p => !original.ContainsKey(p))) throw new ArgumentException("Choose entries from the cleanup preview.");
        // Re-scan all saved assets and current history before deleting any selection.
        var fresh = Scan(plan.ProjectFile, currentHistory, plan.FmlOnly, plan.Owner,
            (plan.ProtectedRoots ?? []).Concat(additionalProtectedRoots ?? [])).Candidates.ToDictionary(c => c.Path, Paths);
        foreach (var path in paths)
            if (!fresh.TryGetValue(path, out var candidate) || candidate.Fingerprint != original[path].Fingerprint || candidate.IsDirectory != original[path].IsDirectory)
                throw new InvalidOperationException("The cleanup preview changed, or an asset is now referenced. Close this window and scan again; nothing was deleted.");
        var root = Path.GetDirectoryName(plan.ProjectFile)!;
        var deleted = new List<string>(); var failures = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                var full = WorkspacePathPolicy.ResolveConfined(root, path, requireExists: true);
                if (original[path].IsDirectory)
                {
                    if (EmptyTreeHash(full) != original[path].Fingerprint) throw new IOException("Directory changed after scanning.");
                    foreach (var directory in EmptyDirectories(full).OrderByDescending(d => d.Length)) Directory.Delete(directory, recursive: false);
                    Directory.Delete(full, recursive: false);
                }
                else
                {
                    if (Hash(full) != original[path].Fingerprint) throw new IOException("File changed after scanning.");
                    File.Delete(full);
                }
                deleted.Add(path);
                var parent = Path.GetDirectoryName(full)!;
                while (!Paths.Equals(parent, Path.Combine(root, "Assets")) && Within(Path.Combine(root, "Assets"), parent))
                {
                    WorkspacePathPolicy.RejectReparsePoints(root, Relative(root, parent), requireExists: true);
                    if (Directory.EnumerateFileSystemEntries(parent).Any()) break;
                    Directory.Delete(parent, recursive: false); parent = Path.GetDirectoryName(parent)!;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { failures.Add(path + ": " + error.Message); }
        }
        return new(deleted, failures);
    }

    private static IEnumerable<string> Files(string root, string[] excluded)
    {
        WorkspacePathPolicy.RejectRootReparsePoints(root, requireExists: true);
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            foreach (var child in Directory.EnumerateFileSystemEntries(folder))
            {
                if (excluded.Any(d => Within(d, child))) continue;
                if (Paths.Equals(folder, root) && Path.GetFileName(child) is "Generated" or ".git") continue;
                WorkspacePathPolicy.RejectReparsePoints(root, Relative(root, child), requireExists: true);
                if (Directory.Exists(child))
                { if (!Path.GetFileName(child).StartsWith(".oasis-", StringComparison.OrdinalIgnoreCase)) pending.Push(child); }
                else yield return child;
            }
        }
    }
    private static IEnumerable<string> EmptyDirectories(string root)
    {
        foreach (var child in Directory.EnumerateDirectories(root))
        {
            WorkspacePathPolicy.RejectReparsePoints(root, Relative(root, child), requireExists: true);
            foreach (var nested in EmptyDirectories(child)) yield return nested;
            if (Directory.EnumerateFiles(child).Any()) throw new IOException("Directory is no longer empty.");
            yield return child;
        }
    }
    private static string EmptyTreeHash(string root)
    {
        if (Directory.EnumerateFiles(root).Any()) throw new IOException("Directory is no longer empty.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', EmptyDirectories(root).Select(p => Relative(root, p)).OrderBy(p => p, StringComparer.Ordinal)))));
    }
    private static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static bool IsLiveImport(string path)
    {
        try { using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static bool Within(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
