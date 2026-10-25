using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace OasisEditor.Automation;

public sealed partial class McpEditorSession
{
    public MachineSnapshot AddMachineObject(Guid documentId, string id, string prefabPath, string? name = null,
        MachineObjectTransform? transform = null, string? parentId = null, bool active = true)
    {
        var entry = GetMachine(documentId);
        var path = Confine(prefabPath);
        if (!string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), Path.Combine(Project.AssetsDirectory, "Object3D"), StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(path) != ProjectAssetPathService.Object3DManifestFileName)
            throw new ArgumentException("Expected a project Prefab package manifest.");
        if (!Object3DDocumentStorage.TryRead(File.ReadAllText(path), out var prefab, out var error)) throw new InvalidDataException(error);
        ValidatePrefabPaths(prefab, Path.GetDirectoryName(path)!);
        var original = entry.Document.GetMachineDocument();
        var instance = new MachineObject3DInstance(id, name ?? id, AssetReference.Project(Path.GetRelativePath(ProjectDirectory, path).Replace('\\', '/')),
            transform ?? MachineObjectTransform.Identity, parentId, active);
        var next = original with { ObjectInstances = [.. original.ObjectInstances, instance] };
        MachineDocumentStorage.Validate(next);
        ValidateMachineAssets(next);
        entry.Document.ExecuteMachineMutation(_ => next, "Add Machine Prefab instance via MCP");
        return InspectMachine(documentId);
    }

    public MachineSnapshot UpdateMachineObject(Guid documentId, string objectId, MachineObjectTransform? transform = null,
        string? parentId = null, bool clearParent = false, bool? active = null, string? name = null)
    {
        var entry = GetMachine(documentId);
        var current = entry.Document.GetMachineDocument().ObjectInstances.FirstOrDefault(i => i.Id == objectId)
            ?? throw new ArgumentException("Unknown Machine object ID.");
        var next = current with { Transform = transform ?? current.Transform, ParentId = clearParent ? null : parentId ?? current.ParentId,
            Active = active ?? current.Active, DisplayName = name ?? current.DisplayName };
        MachineDocumentStorage.Validate(entry.Document.GetMachineDocument() with { ObjectInstances = entry.Document.GetMachineDocument().ObjectInstances.Select(i => i.Id == objectId ? next : i).ToArray() });
        if (!entry.Document.UpdateMachineObjectInstance(objectId, next, "Edit Machine object placement via MCP")) throw new InvalidOperationException("Invalid Machine object edit.");
        return InspectMachine(documentId);
    }

    /// <summary>Adds or replaces a named Prefab dependency without creating a placed instance.</summary>
    public MachineSnapshot DeclareMachinePrefab(Guid documentId, string alias, string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("Prefab alias is required.", nameof(alias));
        var entry = GetMachine(documentId);
        var path = Confine(prefabPath);
        if (!string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), Path.Combine(Project.AssetsDirectory, "Object3D"), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(path), ProjectAssetPathService.Object3DManifestFileName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected a project Prefab package manifest under Assets/Object3D/<Name>.", nameof(prefabPath));
        if (!Object3DDocumentStorage.TryRead(File.ReadAllText(path), out var prefab, out var error)) throw new InvalidDataException(error);
        ValidatePrefabPaths(prefab, Path.GetDirectoryName(path)!);

        var reference = AssetReference.Project(Path.GetRelativePath(ProjectDirectory, path).Replace('\\', '/'));
        var current = entry.Document.GetMachineDocument();
        if (current.PrefabDependencies.FirstOrDefault(dependency => string.Equals(dependency.Alias, alias, StringComparison.Ordinal)) is { } existing
            && existing == new MachinePrefabDeclaration(alias, reference)) return InspectMachine(documentId);
        var next = current with
        {
            PrefabDependencies = [.. current.PrefabDependencies.Where(dependency => !string.Equals(dependency.Alias, alias, StringComparison.Ordinal)), new MachinePrefabDeclaration(alias, reference)]
        };
        MachineDocumentStorage.Validate(next);
        ValidateMachineAssets(next);
        entry.Document.ExecuteMachineMutation(_ => next, "Declare Machine Prefab dependency via MCP");
        return InspectMachine(documentId);
    }

    /// <summary>Adds or replaces a named Machine-space oriented rectangle used by geometry queries.</summary>
    public MachineSnapshot SetMachineGeometrySurface(Guid documentId, MachineGeometrySurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var entry = GetMachine(documentId);
        var current = entry.Document.GetMachineDocument();
        var next = current with
        {
            GeometrySurfaces = [.. current.GeometrySurfaces.Where(existing => !string.Equals(existing.Id, surface.Id, StringComparison.Ordinal)), surface]
        };
        MachineDocumentStorage.Validate(next);
        if (current.GeometrySurfaces.SequenceEqual(next.GeometrySurfaces)) return InspectMachine(documentId);
        entry.Document.ExecuteMachineMutation(_ => next, "Set Machine geometry surface via MCP");
        return InspectMachine(documentId);
    }

    public MachineSnapshot SetMachineAnchor(Guid documentId, MachineAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var entry = GetMachine(documentId);
        var current = entry.Document.GetMachineDocument();
        var existing = current.Anchors.FirstOrDefault(value => string.Equals(value.Id, anchor.Id, StringComparison.Ordinal));
        if (existing == anchor) return InspectMachine(documentId);
        var next = current with { Anchors = existing is null
            ? [.. current.Anchors, anchor]
            : current.Anchors.Select(value => value.Id == anchor.Id ? anchor : value).ToArray() };
        MachineDocumentStorage.Validate(next);
        entry.Document.ExecuteMachineMutation(_ => next, existing is null ? "Add Machine anchor via MCP" : "Set Machine anchor via MCP");
        return InspectMachine(documentId);
    }

    public MachineSnapshot RemoveMachineObject(Guid documentId, string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Machine object ID is required.", nameof(objectId));
        var entry = GetMachine(documentId);
        if (!entry.Document.GetMachineDocument().ObjectInstances.Any(instance => string.Equals(instance.Id, objectId, StringComparison.Ordinal)))
            throw new ArgumentException("Machine object ID does not exist in this document.", nameof(objectId));
        entry.Document.RemoveMachineObjectInstance(objectId);
        return InspectMachine(documentId);
    }

    public MachineSnapshot SetMachineBehavior(Guid documentId, string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Encoding.UTF8.GetByteCount(source) > 256 * 1024) throw new ArgumentException("Machine behavior source cannot exceed 256 KiB.", nameof(source));
        var entry = GetMachine(documentId);
        var machine = entry.Document.GetMachineDocument();
        if (machine.Behavior is null || machine.Runtime is not OasisRuntimeDefinition)
            throw new InvalidOperationException("Machine behavior must be enabled with Oasis runtime before editing its source.");
        if (entry.Document.IsMachineBehaviorSourceMissing)
            throw new InvalidOperationException("behavior.oasis is missing from the Machine package; replace the source file before editing it through MCP.");
        var previous = entry.Document.MachineBehaviorSource;
        if (string.Equals(previous, source, StringComparison.Ordinal)) return InspectMachine(documentId);
        entry.Document.CommandService.Execute(new SetMachineBehaviorSourceCommand(entry.Document, previous, source));
        return InspectMachine(documentId);
    }

    /// <summary>Updates a Machine-owned anchor through the document's undoable command service.</summary>
    public MachineSnapshot UpdateMachineAnchor(Guid documentId, string anchorId,
        MachineVector3? position = null, MachineVector3? rotation = null)
    {
        if (string.IsNullOrWhiteSpace(anchorId)) throw new ArgumentException("Anchor ID is required.", nameof(anchorId));
        ValidateVector(position, nameof(position));
        ValidateVector(rotation, nameof(rotation));

        var entry = GetMachine(documentId);
        var original = entry.Document.GetMachineDocument().Anchors
            .FirstOrDefault(anchor => string.Equals(anchor.Id, anchorId, StringComparison.Ordinal))
            ?? throw new ArgumentException("Anchor ID does not exist in this Machine document.", nameof(anchorId));
        if (position is null && rotation is null) return InspectMachine(documentId);

        var replacement = original with
        {
            Position = position ?? original.Position,
            Rotation = rotation ?? original.Rotation
        };
        if (replacement == original) return InspectMachine(documentId);
        if (!entry.Document.UpdateMachineAnchor(anchorId, replacement, "Edit Machine anchor via MCP"))
            throw new InvalidOperationException("Machine anchor update was rejected.");
        return InspectMachine(documentId);
    }

    public MachineSnapshot UndoMachine(Guid documentId)
    {
        var entry = GetMachine(documentId);
        entry.Document.CommandService.TryUndo();
        return InspectMachine(documentId);
    }

    public MachineSnapshot RedoMachine(Guid documentId)
    {
        var entry = GetMachine(documentId);
        entry.Document.CommandService.TryRedo();
        return InspectMachine(documentId);
    }

    /// <summary>Saves the authored Machine package after checking that neither persisted source file changed externally.</summary>
    public MachineSnapshot SaveMachine(Guid documentId)
    {
        var entry = GetMachine(documentId);
        var behaviorPath = Confine(Path.Combine(Path.GetDirectoryName(entry.Path)!, MachineBehaviorDefinition.CanonicalSourcePath));
        if (!File.Exists(entry.Path) || Hash(entry.Path) != entry.Hash
            || HashOptional(behaviorPath) != entry.BehaviorHash)
            throw new IOException("Machine package changed on disk; refusing to overwrite external changes. Reopen it in a fresh session.");

        ValidateMachineAssets(entry.Document.GetMachineDocument());
        new DocumentSaveService().SaveDocument(entry.Document, entry.Path, Project).ApplyTo(entry.Document);

        // Apply the new persisted baseline only after the save service finishes successfully.
        _machines[documentId] = (entry.Document, entry.Path, Hash(entry.Path), HashOptional(behaviorPath));
        return InspectMachine(documentId);
    }

    private static void ValidateVector(MachineVector3? value, string parameterName)
    {
        if (value is not null && (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z)))
            throw new ArgumentException("Anchor coordinates must be finite numbers.", parameterName);
    }

    private static string? HashOptional(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
        : null;

    private sealed class SetMachineBehaviorSourceCommand : OasisEditor.Commands.IDocumentCommand, OasisEditor.Commands.IExecutionTrackedCommand
    {
        private readonly DocumentTabViewModel _document;
        private readonly string _previous;
        private readonly string _next;

        public SetMachineBehaviorSourceCommand(DocumentTabViewModel document, string previous, string next)
        { _document = document; _previous = previous; _next = next; }

        public Guid DocumentId => _document.DocumentId;
        public string Description => "Edit Machine behavior source via MCP";
        public bool WasExecuted { get; private set; }
        public void Execute()
        {
            if (string.Equals(_document.MachineBehaviorSource, _next, StringComparison.Ordinal)) return;
            _document.MachineBehaviorSource = _next;
            WasExecuted = true;
        }
        public void Undo() => _document.MachineBehaviorSource = _previous;
    }
}
