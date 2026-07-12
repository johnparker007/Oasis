namespace OasisEditor;

public sealed class BulkDeleteSelectionCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
{
    private readonly Guid _documentId;
    private readonly DocumentTabViewModel _document;
    private readonly EditorSelectionItem[] _selectionSnapshot;
    private readonly List<DeletedPanelElement> _deletedPanelElements = [];
    private readonly List<DeletedFaceElement> _deletedFaceElements = [];

    public BulkDeleteSelectionCommand(Guid documentId, DocumentTabViewModel document, IEnumerable<EditorSelectionItem> selectionSnapshot)
    {
        _documentId = documentId;
        _document = document;
        _selectionSnapshot = selectionSnapshot.Where(item => item.IsValid).Distinct().ToArray();
    }

    public Guid DocumentId => _documentId;
    public string Description => "Delete selected components";
    public bool WasExecuted { get; private set; }

    public void Execute()
    {
        WasExecuted = false;
        CaptureDeletedSnapshotsIfNeeded();
        if (_deletedPanelElements.Count == 0 && _deletedFaceElements.Count == 0)
        {
            return;
        }

        var changed = false;
        if (_deletedPanelElements.Count > 0)
        {
            var ids = _deletedPanelElements.Select(item => item.Element.ObjectId).ToHashSet(StringComparer.Ordinal);
            var elements = _document.GetPanelElements()
                .Where(element => !ids.Contains(element.ObjectId))
                .Select(element => PanelElementModelCloner.Clone(element))
                .ToArray();
            _document.SetPanelElements(elements, CreateStructureChange(_document));
            changed = true;
        }

        if (_deletedFaceElements.Count > 0)
        {
            var ids = _deletedFaceElements.Select(item => item.Element.ObjectId).ToHashSet(StringComparer.Ordinal);
            var elements = _document.GetFaceElements()
                .Where(element => !ids.Contains(element.ObjectId))
                .Select(element => FaceElementModelCloner.Clone(element))
                .ToArray();
            _document.SetFaceElements(elements, CreateStructureChange(_document));
            changed = true;
        }

        if (changed)
        {
            _document.MarkDirty();
            WasExecuted = true;
        }
    }

    public void Undo()
    {
        var changed = false;
        if (_deletedPanelElements.Count > 0)
        {
            var elements = _document.GetPanelElements().Select(element => PanelElementModelCloner.Clone(element)).ToList();
            foreach (var deleted in _deletedPanelElements.OrderBy(item => item.Index))
            {
                if (elements.Any(element => string.Equals(element.ObjectId, deleted.Element.ObjectId, StringComparison.Ordinal)))
                {
                    continue;
                }

                elements.Insert(Math.Clamp(deleted.Index, 0, elements.Count), PanelElementModelCloner.Clone(deleted.Element));
                changed = true;
            }
            if (changed)
            {
                _document.SetPanelElements(elements, CreateStructureChange(_document));
            }
        }

        var faceChanged = false;
        if (_deletedFaceElements.Count > 0)
        {
            var elements = _document.GetFaceElements().Select(element => FaceElementModelCloner.Clone(element)).ToList();
            foreach (var deleted in _deletedFaceElements.OrderBy(item => item.Index))
            {
                if (elements.Any(element => string.Equals(element.ObjectId, deleted.Element.ObjectId, StringComparison.Ordinal)))
                {
                    continue;
                }

                elements.Insert(Math.Clamp(deleted.Index, 0, elements.Count), FaceElementModelCloner.Clone(deleted.Element));
                faceChanged = true;
            }
            if (faceChanged)
            {
                _document.SetFaceElements(elements, CreateStructureChange(_document));
            }
        }

        if (changed || faceChanged)
        {
            _document.MarkDirty();
        }
    }

    private void CaptureDeletedSnapshotsIfNeeded()
    {
        if (_deletedPanelElements.Count > 0 || _deletedFaceElements.Count > 0)
        {
            return;
        }

        var selectedPanelIds = _selectionSnapshot
            .Where(item => item.Domain == EditorSelectionDomain.PanelElement)
            .Select(item => item.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedPanelIds.Count > 0)
        {
            var panelElements = _document.GetPanelElements();
            for (var i = 0; i < panelElements.Count; i++)
            {
                if (selectedPanelIds.Contains(panelElements[i].ObjectId))
                {
                    _deletedPanelElements.Add(new DeletedPanelElement(i, PanelElementModelCloner.Clone(panelElements[i])));
                }
            }
        }

        var selectedFaceIds = _selectionSnapshot
            .Where(item => item.Domain == EditorSelectionDomain.FaceElement)
            .Select(item => item.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedFaceIds.Count > 0)
        {
            var faceElements = _document.GetFaceElements();
            for (var i = 0; i < faceElements.Count; i++)
            {
                if (selectedFaceIds.Contains(faceElements[i].ObjectId))
                {
                    _deletedFaceElements.Add(new DeletedFaceElement(i, FaceElementModelCloner.Clone(faceElements[i])));
                }
            }
        }
    }

    private static PanelChangeEvent CreateStructureChange(DocumentTabViewModel document)
    {
        return new PanelChangeEvent(
            document.DocumentId,
            null,
            PanelChangeProperties.Structure,
            AffectsCanvas: true,
            AffectsHierarchy: true,
            AffectsInspectorRows: true,
            AffectsPersistence: true);
    }

    private sealed record DeletedPanelElement(int Index, PanelElementModel Element);
    private sealed record DeletedFaceElement(int Index, FaceElementModel Element);
}
