using System.Security.Cryptography;
using System.IO;

namespace OasisEditor.Automation;

public sealed partial class McpEditorSession
{
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
}
