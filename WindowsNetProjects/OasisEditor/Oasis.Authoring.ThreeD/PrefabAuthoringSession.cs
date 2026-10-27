using System.Text.Json;

namespace OasisEditor;

/// <summary>Portable authoring for schema 2 Object3D Prefab hierarchy documents.</summary>
public sealed class PrefabAuthoringSession : ThreeDAuthoringSessionBase
{
    private string _payload = string.Empty;
    private Object3DDocument _document = Object3DDocument.Create("Object3D");

    public PrefabAuthoringSession(AuthoringSession projectSession)
        : base(projectSession, "Object3D", ProjectAssetPathService.Object3DManifestFileName) { }

    public Object3DDocument Document => Clone(_document);
    public event Action? Changed;
    protected override string CurrentPayload => _payload;

    public void Open(string manifestPath)
    {
        EnsureCanReplaceDocument();
        var manifest = ResolveManifest(manifestPath);
        var json = File.ReadAllText(manifest);
        if (!Object3DDocumentStorage.TryRead(json, out var document, out var error)) throw new InvalidDataException(error);
        var candidate = Clone(document);
        var package = Path.GetDirectoryName(manifest)!;
        ValidatePackage(candidate, package);
        var payload = Object3DDocumentStorage.Serialize(candidate);
        var hashes = CapturePackageHashes(package);
        SetOpen(manifest, payload, hashes);
        _document = candidate;
        _payload = payload;
        Changed?.Invoke();
    }

    public string Create(string name, string? sourceProjectRelativePath = null)
    {
        EnsureCanReplaceDocument();
        if (sourceProjectRelativePath is not null) _ = ResolveSource(sourceProjectRelativePath);
        using var reservation = ReservePackage(name);
        var package = reservation.StagingDirectory;
        var expected = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var document = Object3DDocument.Create(name);
        document = document with { Root = document.Root with { Components = document.Root.Components.Where(c => c.Kind == Object3DComponentDefinition.TransformKind).ToArray() } };
        if (sourceProjectRelativePath is not null)
        {
            var path = CopyModel(sourceProjectRelativePath, package, expected);
            var model = Object3DComponentDefinition.CreateModel(new Object3DModelDefinition(path, 1, "Y"));
            document = document with { Root = document.Root.WithComponent(model) };
        }
        ValidatePackage(document, package);
        var payload = Object3DDocumentStorage.Serialize(document);
        var stagedManifest = Path.Combine(package, ProjectAssetPathService.Object3DManifestFileName);
        WriteNewFile(stagedManifest, payload);
        var hashes = CaptureAfterOwnedFileWrite(package, expected, ProjectAssetPathService.Object3DManifestFileName, stagedManifest);
        EnsureCanReplaceDocument();
        reservation.Commit();
        var manifest = Path.Combine(reservation.FinalDirectory, ProjectAssetPathService.Object3DManifestFileName);
        SetCreated(manifest, payload, hashes);
        _document = document;
        _payload = payload;
        Changed?.Invoke();
        return manifest;
    }

    public string ImportModel(string nodeId, string sourceProjectRelativePath, double scale = 1, string upAxis = "Y")
    {
        if (FindNode(_document.Root, nodeId) is null) throw new KeyNotFoundException($"Prefab node '{nodeId}' was not found.");
        ValidateModel(new Object3DModelDefinition("placeholder.glb", scale, upAxis));
        EnsureEditingAsset();
        var path = CopyModel(sourceProjectRelativePath, PackageDirectory!);
        SetModel(nodeId, new Object3DModelDefinition(path, scale, upAxis));
        return path;
    }

    public bool RenameNode(string nodeId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Node name cannot be blank.", nameof(name));
        return EditNode(nodeId, node => node with { Name = name.Trim() });
    }

    public bool SetTransform(string nodeId, MachineObjectTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return EditNode(nodeId, node => node with { Transform = transform });
    }

    public string AddNode(string parentNodeId, string name, MachineObjectTransform? transform = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Node name cannot be blank.", nameof(name));
        var id = Guid.NewGuid().ToString("D");
        var child = new Object3DNodeDefinition(id, name.Trim(), [Object3DComponentDefinition.CreateTransform(transform)], []);
        var parent = false;
        var root = Rewrite(_document.Root, node =>
        {
            if (node.Id != parentNodeId) return node;
            parent = true;
            return node with { Children = [.. node.Children, child] };
        });
        if (!parent) throw new KeyNotFoundException($"Prefab node '{parentNodeId}' was not found.");
        Commit(_document with { Root = root });
        return id;
    }

    public bool RemoveNode(string nodeId)
    {
        if (_document.Root.Id == nodeId) throw new InvalidOperationException("The Prefab root cannot be removed.");
        var removed = false;
        var root = Rewrite(_document.Root, node =>
        {
            var children = node.Children.Where(child => child.Id != nodeId).ToArray();
            if (children.Length != node.Children.Length) removed = true;
            return node with { Children = children.Select(RemoveDescendant).ToArray() };
        });
        if (!removed) throw new KeyNotFoundException($"Prefab node '{nodeId}' was not found.");
        return Commit(_document with { Root = root });
    }

    public bool ReparentNode(string nodeId, string newParentId, int? index = null)
    {
        if (_document.Root.Id == nodeId) throw new InvalidOperationException("The Prefab root cannot be reparented.");
        var subtree = FindNode(_document.Root, nodeId) ?? throw new KeyNotFoundException($"Prefab node '{nodeId}' was not found.");
        if (FindNode(subtree, newParentId) is not null) throw new InvalidOperationException("A Prefab node cannot be reparented beneath itself or its descendants.");
        var root = RemoveFromTree(_document.Root, nodeId, out _);
        var inserted = false;
        root = Rewrite(root, node =>
        {
            if (node.Id != newParentId) return node;
            var children = node.Children.ToList();
            var at = index ?? children.Count;
            if (at < 0 || at > children.Count) throw new ArgumentOutOfRangeException(nameof(index), "Child index must be between zero and the parent child count.");
            children.Insert(at, subtree); inserted = true;
            return node with { Children = children.ToArray() };
        });
        if (!inserted) throw new KeyNotFoundException($"Prefab parent node '{newParentId}' was not found.");
        return Commit(_document with { Root = root });
    }

    public bool ReorderChildren(string parentNodeId, IReadOnlyList<string> orderedChildIds)
    {
        ArgumentNullException.ThrowIfNull(orderedChildIds);
        var found = false;
        var root = Rewrite(_document.Root, node =>
        {
            if (node.Id != parentNodeId) return node;
            found = true;
            if (orderedChildIds.Count != node.Children.Length || orderedChildIds.Distinct(StringComparer.Ordinal).Count() != orderedChildIds.Count
                || !orderedChildIds.ToHashSet(StringComparer.Ordinal).SetEquals(node.Children.Select(child => child.Id)))
                throw new ArgumentException("Reorder must list every current child ID exactly once.", nameof(orderedChildIds));
            var byId = node.Children.ToDictionary(child => child.Id, StringComparer.Ordinal);
            return node with { Children = orderedChildIds.Select(id => byId[id]).ToArray() };
        });
        if (!found) throw new KeyNotFoundException($"Prefab node '{parentNodeId}' was not found.");
        return Commit(_document with { Root = root });
    }

    public bool SetModel(string nodeId, Object3DModelDefinition model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ValidateModel(model);
        return EditNode(nodeId, node => node.WithComponent(Object3DComponentDefinition.CreateModel(model)));
    }

    public bool SetCollider(string nodeId, Object3DColliderDefinition collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        return EditNode(nodeId, node => node.WithComponent(Object3DComponentDefinition.CreateCollider(collider)));
    }

    public bool SetRigidbody(string nodeId, Object3DRigidbodyDefinition rigidbody)
    {
        ArgumentNullException.ThrowIfNull(rigidbody);
        return EditNode(nodeId, node => node.WithComponent(Object3DComponentDefinition.CreateRigidbody(rigidbody)));
    }

    public bool AddComponent(string nodeId, string kind) => kind switch
    {
        Object3DComponentDefinition.ModelKind => SetModel(nodeId, new("model.glb", 1, "Y")),
        Object3DComponentDefinition.ColliderKind => SetCollider(nodeId, Object3DColliderDefinition.None),
        Object3DComponentDefinition.RigidbodyKind => SetRigidbody(nodeId, new(Object3DRigidbodyMode.Static)),
        _ => throw new ArgumentException("Only Model, Collider, and Rigidbody components can be added; every node already has a Transform.", nameof(kind))
    };

    public bool RemoveComponent(string nodeId, string kind)
    {
        if (kind == Object3DComponentDefinition.TransformKind) throw new InvalidOperationException("Every Prefab node must retain its Transform component.");
        var removed = false;
        var root = Rewrite(_document.Root, node =>
        {
            if (node.Id != nodeId) return node;
            var next = node.WithoutComponent(kind);
            removed = next.Components.Length != node.Components.Length;
            return next;
        });
        if (FindNode(_document.Root, nodeId) is null) throw new KeyNotFoundException($"Prefab node '{nodeId}' was not found.");
        return removed && Commit(_document with { Root = root });
    }

    public void Save()
    {
        EnsureEditingAsset();
        ValidatePackage(_document, PackageDirectory!);
        _payload = Object3DDocumentStorage.Serialize(_document);
        SavePayload(_payload);
        Changed?.Invoke();
    }

    protected override void SetCurrentPayload(string payload)
    {
        if (!Object3DDocumentStorage.TryRead(payload, out var document, out var error)) throw new InvalidDataException(error);
        _document = Clone(document); _payload = payload; Changed?.Invoke();
    }

    private bool EditNode(string nodeId, Func<Object3DNodeDefinition, Object3DNodeDefinition> edit) => EditNode(nodeId, edit, out _);
    private bool EditNode(string nodeId, Func<Object3DNodeDefinition, Object3DNodeDefinition> edit, out bool found)
    {
        found = FindNode(_document.Root, nodeId) is not null;
        if (!found) throw new KeyNotFoundException($"Prefab node '{nodeId}' was not found.");
        return Commit(_document with { Root = Rewrite(_document.Root, node => node.Id == nodeId ? edit(node) : node) });
    }

    private bool Commit(Object3DDocument candidate)
    {
        EnsureEditingAsset();
        Validate(candidate);
        var payload = Object3DDocumentStorage.Serialize(candidate);
        if (payload == _payload) return false;
        CommitPayload(payload); return true;
    }

    private static void ValidatePackage(Object3DDocument document, string packageDirectory)
    {
        Validate(document);
        foreach (var model in EnumerateModels(document.Root))
        {
            var path = WorkspacePathPolicy.ResolveConfined(packageDirectory, model.Path, requireExists: true);
            WorkspacePathPolicy.RejectReparsePoints(packageDirectory, model.Path, requireExists: true);
            if (!File.Exists(path)) throw new InvalidDataException($"Prefab model '{model.Path}' is missing.");
            ValidatePackageModelGlb(path);
        }
        Object3DValidationService.Validate(document, packageDirectory);
    }

    private static void Validate(Object3DDocument document) => Object3DValidationService.Validate(document);
    private static void ValidateModel(Object3DModelDefinition model)
    {
        if (!double.IsFinite(model.Scale) || model.Scale <= 0) throw new ArgumentOutOfRangeException(nameof(model), "Model scale must be positive and finite.");
        if (model.UpAxis is not ("X" or "Y" or "Z")) throw new ArgumentException("Model up-axis must be X, Y, or Z.", nameof(model));
    }
    private static Object3DDocument Clone(Object3DDocument document) => Object3DDocumentStorage.TryRead(Object3DDocumentStorage.Serialize(document), out var clone, out var error) ? clone : throw new InvalidDataException(error);
    protected override void ResetDocument()
    {
        _document = Object3DDocument.Create("Object3D");
        _payload = string.Empty;
        Changed?.Invoke();
    }
    private static IEnumerable<Object3DModelDefinition> EnumerateModels(Object3DNodeDefinition node)
    {
        foreach (var component in node.Components) if (component.Model is { } model) yield return model;
        foreach (var child in node.Children) foreach (var model in EnumerateModels(child)) yield return model;
    }
    private static Object3DNodeDefinition? FindNode(Object3DNodeDefinition node, string id) => node.Id == id ? node : node.Children.Select(child => FindNode(child, id)).FirstOrDefault(found => found is not null);
    private static Object3DNodeDefinition Rewrite(Object3DNodeDefinition node, Func<Object3DNodeDefinition, Object3DNodeDefinition> update)
    {
        var rewritten = node with { Children = node.Children.Select(child => Rewrite(child, update)).ToArray() };
        return update(rewritten);
    }
    private static Object3DNodeDefinition RemoveFromTree(Object3DNodeDefinition node, string id, out bool removed)
    {
        removed = false;
        var children = new List<Object3DNodeDefinition>();
        foreach (var child in node.Children)
        {
            if (child.Id == id) { removed = true; continue; }
            var rewritten = RemoveFromTree(child, id, out var childRemoved);
            removed |= childRemoved; children.Add(rewritten);
        }
        return node with { Children = children.ToArray() };
    }
    private static Object3DNodeDefinition RemoveDescendant(Object3DNodeDefinition node) => node with { Children = node.Children.Select(RemoveDescendant).ToArray() };
}
