using OasisEditor.Commands;

namespace OasisEditor.Features.MfmeImport;

internal sealed class ImportMfmeExtractCommand : IDocumentCommand, IExecutionTrackedCommand
{
    private readonly Guid _documentId;
    private readonly DocumentTabViewModel _document;
    private readonly IReadOnlyList<PanelElementModel> _sourceElements;
    private IReadOnlyList<PanelElementModel>? _importedElements;
    private int _insertIndex;

    public ImportMfmeExtractCommand(
        Guid documentId,
        DocumentTabViewModel document,
        IReadOnlyList<PanelElementModel> sourceElements)
    {
        _documentId = documentId;
        _document = document;
        _sourceElements = sourceElements ?? [];
    }

    public Guid DocumentId => _documentId;

    public string Description => "Import MFME extract";

    public bool WasExecuted { get; private set; }

    public void Execute()
    {
        WasExecuted = false;

        if (_sourceElements.Count == 0)
        {
            return;
        }

        var currentElements = _document.GetPanelElements().ToList();
        if (_importedElements is null)
        {
            _importedElements = MaterializeImportedElements(_sourceElements, currentElements);
            _insertIndex = currentElements.Count;
        }

        if (_importedElements.Count == 0)
        {
            return;
        }

        var importedIds = new HashSet<string>(_importedElements.Select(element => element.ObjectId), StringComparer.Ordinal);
        if (currentElements.Any(element => importedIds.Contains(element.ObjectId)))
        {
            return;
        }

        var insertIndex = Math.Clamp(_insertIndex, 0, currentElements.Count);
        currentElements.InsertRange(insertIndex, _importedElements.Select(static element => CloneElement(element)));
        _document.SetPanelElements(currentElements);
        _document.MarkDirty();
        WasExecuted = true;
    }

    public void Undo()
    {
        if (_importedElements is null || _importedElements.Count == 0)
        {
            return;
        }

        var importedIds = new HashSet<string>(_importedElements.Select(element => element.ObjectId), StringComparer.Ordinal);
        var elements = _document.GetPanelElements().ToList();
        var removed = elements.RemoveAll(element => importedIds.Contains(element.ObjectId)) > 0;
        if (!removed)
        {
            return;
        }

        _document.SetPanelElements(elements);
        _document.MarkDirty();
    }

    private static IReadOnlyList<PanelElementModel> MaterializeImportedElements(
        IReadOnlyList<PanelElementModel> sourceElements,
        IReadOnlyList<PanelElementModel> existingElements)
    {
        var existingIds = new HashSet<string>(existingElements.Select(element => element.ObjectId), StringComparer.Ordinal);
        var imported = new List<PanelElementModel>(sourceElements.Count);

        foreach (var source in sourceElements)
        {
            var objectId = string.IsNullOrWhiteSpace(source.ObjectId) || existingIds.Contains(source.ObjectId)
                ? BuildUniqueObjectId(existingIds)
                : source.ObjectId;

            existingIds.Add(objectId);
            imported.Add(CloneElement(source, objectId));
        }

        return imported;
    }

    private static string BuildUniqueObjectId(IReadOnlySet<string> existingIds)
    {
        string candidate;
        do
        {
            candidate = Guid.NewGuid().ToString("N");
        } while (existingIds.Contains(candidate));

        return candidate;
    }

    private static PanelElementModel CloneElement(PanelElementModel source, string? objectId = null)
    {
        return new PanelElementModel
        {
            ObjectId = objectId ?? source.ObjectId,
            Name = source.Name,
            Kind = source.Kind,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            AssetPath = source.AssetPath,
            SecondaryAssetPath = source.SecondaryAssetPath,
            DisplayNumber = source.DisplayNumber,
            OnColorHex = source.OnColorHex,
            OffColorHex = source.OffColorHex,
            TextColorHex = source.TextColorHex,
            DisplayText = source.DisplayText,
            IsReversed = source.IsReversed,
            Stops = source.Stops,
            VisibleScale = source.VisibleScale,
            ImportSource = source.ImportSource is null
                ? null
                : new PanelElementImportSourceModel
                {
                    Format = source.ImportSource.Format,
                    Reference = source.ImportSource.Reference
                }
        };
    }
}
