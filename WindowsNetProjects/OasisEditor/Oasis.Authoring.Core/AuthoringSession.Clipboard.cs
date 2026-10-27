using System.Text.Json;

namespace OasisEditor;

public sealed partial class AuthoringSession
{
    private PanelElementFile[] _panelClipboard = [];
    private string? _panelClipboardProjectRoot;

    /// <summary>Whether this authoring session has a reusable Panel2D clipboard payload.</summary>
    public bool CanPasteClipboard => _panelClipboard.Length > 0 && _panelClipboardProjectRoot is not null && _projectDirectory is not null
        && PathComparer.Equals(_panelClipboardProjectRoot, Path.GetFullPath(RequireProject()));

    /// <summary>Copy the selected Panel2D elements in stacking order to this session's internal clipboard.</summary>
    public bool CopySelectedToClipboard()
    {
        var document = RequireDocument();
        var selected = _selectedIds.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0) return false;
        _panelClipboard = document.Elements.Where(element => selected.Contains(element.ObjectId))
            .Select(CloneElement).ToArray();
        _panelClipboardProjectRoot = Path.GetFullPath(RequireProject());
        return _panelClipboard.Length > 0;
    }

    /// <summary>Copy and delete the current selection as one normal document history edit.</summary>
    public bool CutSelectedToClipboard()
    {
        if (!CopySelectedToClipboard()) return false;
        return DeleteSelected();
    }

    /// <summary>Paste the internal clipboard into the active panel with fresh IDs and an offset.</summary>
    public IReadOnlyList<string> PasteClipboard(double offsetX = 16, double offsetY = 16)
    {
        ValidateGeometry(offsetX, offsetY, null, null);
        if (_panelClipboard.Length == 0) return [];
        var document = RequireDocument();
        var projectRoot = Path.GetFullPath(RequireProject());
        if (_panelClipboardProjectRoot is null || !PathComparer.Equals(_panelClipboardProjectRoot, projectRoot))
            throw new InvalidOperationException("The copied elements belong to another project. Copy them again before pasting.");
        var existing = document.Elements.Select(element => element.ObjectId).ToHashSet(StringComparer.Ordinal);
        var copies = new PanelElementFile[_panelClipboard.Length];
        for (var i = 0; i < _panelClipboard.Length; i++)
        {
            var source = CloneElement(_panelClipboard[i]);
            ValidateClipboardAssetReferences(projectRoot, source);
            var id = Guid.NewGuid().ToString("N");
            while (!existing.Add(id)) id = Guid.NewGuid().ToString("N");
            var x = source.X + offsetX;
            var y = source.Y + offsetY;
            ValidateGeometry(x, y, source.Width, source.Height);
            copies[i] = source with { ObjectId = id, X = x, Y = y };
        }
        Commit(document with { Elements = [.. document.Elements, .. copies] }, notify: false);
        _selectedIds = copies.Select(copy => copy.ObjectId).ToList();
        _selectedId = _selectedIds.FirstOrDefault();
        StorePanelState();
        Changed?.Invoke();
        return _selectedIds.ToArray();
    }

    /// <summary>Move the selected elements to the top of the stacking order, preserving their relative order.</summary>
    public bool BringSelectionToFront()
        => MoveSelectionToEnd();

    /// <summary>Move the selected elements to the bottom of the stacking order, preserving their relative order.</summary>
    public bool SendSelectionToBack()
        => MoveSelectionToStart();

    private bool MoveSelectionToEnd()
    {
        var document = RequireDocument();
        var selected = _selectedIds.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0) return false;
        var reordered = document.Elements.Where(element => !selected.Contains(element.ObjectId))
            .Concat(document.Elements.Where(element => selected.Contains(element.ObjectId))).ToArray();
        if (reordered.SequenceEqual(document.Elements)) return false;
        Commit(document with { Elements = reordered });
        return true;
    }

    private bool MoveSelectionToStart()
    {
        var document = RequireDocument();
        var selected = _selectedIds.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0) return false;
        var reordered = document.Elements.Where(element => selected.Contains(element.ObjectId))
            .Concat(document.Elements.Where(element => !selected.Contains(element.ObjectId))).ToArray();
        if (reordered.SequenceEqual(document.Elements)) return false;
        Commit(document with { Elements = reordered });
        return true;
    }

    private static void ValidateClipboardAssetReferences(string root, PanelElementFile element)
    {
        foreach (var reference in new[]
        {
            element.AssetPath, element.SecondaryAssetPath, element.ReelLampTransmissionMaskAssetPath,
            element.Native?.AssetPath, element.Native?.SecondaryAssetPath
        })
        {
            if (string.IsNullOrWhiteSpace(reference)) continue;
            // References are project-relative, so they remain valid when pasted between panels
            // in this project. Reject rooted/traversing paths instead of carrying unsafe refs.
            _ = WorkspacePathPolicy.ResolveConfined(root, reference, requireExists: false);
        }
    }

    private static PanelElementFile CloneElement(PanelElementFile element)
    {
        var json = JsonSerializer.Serialize(element);
        return JsonSerializer.Deserialize<PanelElementFile>(json)
            ?? throw new InvalidDataException("The selected Panel2D element could not be copied.");
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
