using System.Text.Json;

namespace OasisEditor;

public sealed partial class AuthoringSession
{
    private sealed class PanelClipboardState
    {
        public PanelElementFile[] Elements = [];
        public PanelGroup[] Groups = [];
        public string[] Order = [];
        public string[] Roots = [];
        public string? ProjectRoot;
    }

    private PanelClipboardState _panelClipboardState = new();
    // Keep the existing local names while allowing paired authoring sessions to share one clipboard instance.
    private PanelElementFile[] _panelClipboard
    {
        get => _panelClipboardState.Elements;
        set => _panelClipboardState.Elements = value;
    }
    private string? _panelClipboardProjectRoot
    {
        get => _panelClipboardState.ProjectRoot;
        set => _panelClipboardState.ProjectRoot = value;
    }

    /// <summary>Shares the workspace clipboard between document sessions for the same project.</summary>
    public void ShareClipboardWith(AuthoringSession destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        EnsureSameProject(_projectFilePath,destination._projectFilePath);
        destination._panelClipboardState=_panelClipboardState;
    }

    /// <summary>Whether this authoring session has a reusable Panel2D clipboard payload.</summary>
    public bool CanPasteClipboard => (_panelClipboard.Length > 0 || _panelClipboardState.Groups.Length>0) && _panelClipboardProjectRoot is not null && _projectDirectory is not null
        && PathComparer.Equals(_panelClipboardProjectRoot, Path.GetFullPath(RequireProject()));

    /// <summary>Copy the selected Panel2D elements in stacking order to this session's internal clipboard.</summary>
    public bool CopySelectedToClipboard()
    {
        var document = RequireDocument();
        if(_selectedIds.Count==0)return false;
        var roots=SelectionRoots(document,_selectedIds);var selected=Descendants(document,roots);var tree=Tree;
        _panelClipboard=document.Elements.Where(e=>selected.Contains(e.ObjectId)).Select(e=>
        {
            var state=tree.Resolve(e.ParentId);var root=e.ParentId is null||!selected.Contains(e.ParentId);
            return (root?WithLocalTransform(CloneElement(e),(e.Transform??PanelTransform.Identity).Then(state.Transform),null):CloneElement(e)) with {
                IsVisible=(e.IsVisible??true)&&(!root||state.Visible),LockTransform=e.LockTransform||(root&&state.Locked)};
        }).ToArray();
        _panelClipboardState.Groups=document.Groups.Where(g=>selected.Contains(g.Id)).Select(g=>
        {
            var root=g.ParentId is null||!selected.Contains(g.ParentId);var state=tree.Resolve(g.Id);
            return root?g with {ParentId=null,Transform=state.Transform,Visible=state.Visible,Locked=state.Locked}:g;
        }).ToArray();
        _panelClipboardState.Order=OrderedIds(document).Where(selected.Contains).ToArray();_panelClipboardState.Roots=roots;
        _panelClipboardProjectRoot=Path.GetFullPath(RequireProject());return true;
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
        if (_panelClipboard.Length == 0 && _panelClipboardState.Groups.Length==0) return [];
        var document=RequireDocument();var projectRoot=Path.GetFullPath(RequireProject());
        if(_panelClipboardProjectRoot is null||!PathComparer.Equals(_panelClipboardProjectRoot,projectRoot))throw new InvalidOperationException("The copied objects belong to another project.");
        var ids=_panelClipboard.Select(e=>e.ObjectId).Concat(_panelClipboardState.Groups.Select(g=>g.Id)).ToDictionary(id=>id,_=>Guid.NewGuid().ToString("N"));
        var elements=_panelClipboard.Select(e=>
        {
            ValidateClipboardAssetReferences(projectRoot,e);
            var copy=CloneElement(e) with {ObjectId=ids[e.ObjectId],SharedSourceSetId=e.SharedSourceSetId is {} shared&&ids.TryGetValue(shared,out var mapped)?mapped:e.SharedSourceSetId,ParentId=e.ParentId is null?null:ids[e.ParentId]};
            if(e.ParentId is null)copy=TranslateLocal(copy,offsetX,offsetY);
            return copy;
        }).ToArray();
        var groups=_panelClipboardState.Groups.Select(g=>g with {Id=ids[g.Id],Control=g.Control?.Input is {} binding?g.Control with {Input=binding with {Id=Guid.NewGuid().ToString("N")}}:g.Control,ParentId=g.ParentId is null?null:ids[g.ParentId],Transform=g.ParentId is null?g.Transform with {X=g.Transform.X+offsetX,Y=g.Transform.Y+offsetY}:g.Transform}).ToArray();
        Commit(document with {Elements=[..document.Elements,..elements],Groups=[..document.Groups,..groups],HierarchyOrder=[..OrderedIds(document),.._panelClipboardState.Order.Select(id=>ids[id])]},notify:false);
        _selectedIds=_panelClipboardState.Roots.Select(id=>ids[id]).ToList();_selectedId=_selectedIds.FirstOrDefault();StorePanelState();Changed?.Invoke();return _selectedIds.ToArray();
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
        if(document.Groups.Length>0||document.HierarchyOrder.Length>0)return MoveHierarchySelection(true,toEnd:true);
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
        if(document.Groups.Length>0||document.HierarchyOrder.Length>0)return MoveHierarchySelection(false,toEnd:true);
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
