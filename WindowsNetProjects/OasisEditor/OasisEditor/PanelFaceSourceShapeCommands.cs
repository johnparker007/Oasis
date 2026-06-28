using OasisEditor.Commands;

namespace OasisEditor;

internal static class PanelFaceSourceShapeCommands
{
    public const string SelectionKind = "faceSourceShape";

    public static PanelSelectionInfo ToSelection(PanelFaceSourceShapeModel shape) => new(shape.Id, SelectionKind, shape.X, shape.Y, shape.Width, shape.Height);

    public static ICommand CreateAddCommand(Guid documentId, DocumentTabViewModel document, PanelFaceSourceShapeModel shape) => new Add(documentId, document, shape);
    public static ICommand CreateUpdateCommand(Guid documentId, DocumentTabViewModel document, PanelFaceSourceShapeModel shape, string description = "Update Face Source Shape") => new Update(documentId, document, shape, description);

    private sealed class Add(Guid documentId, DocumentTabViewModel document, PanelFaceSourceShapeModel shape) : IDocumentCommand, IExecutionTrackedCommand
    {
        public string Description => "Add Face Source Shape";
        public Guid DocumentId => documentId;
        public bool WasExecuted { get; private set; }
        public void Execute()
        {
            if (document.DocumentId != documentId || document.GetPanelFaceSourceShapes().Any(s => s.Id == shape.Id)) return;
            var shapes = document.GetPanelFaceSourceShapes().Concat([shape]).ToArray();
            document.SetPanelFaceSourceShapes(shapes, new PanelChangeEvent(documentId, shape.Id, PanelChangeProperties.Structure, true, true, true, true));
            document.HierarchySelectedPanelSelection = ToSelection(shape);
            document.MarkDirty();
            WasExecuted = true;
        }
        public void Undo()
        {
            var shapes = document.GetPanelFaceSourceShapes().Where(s => s.Id != shape.Id).ToArray();
            document.SetPanelFaceSourceShapes(shapes, new PanelChangeEvent(documentId, shape.Id, PanelChangeProperties.Structure, true, true, true, true));
            WasExecuted = false;
        }
    }

    private sealed class Update(Guid documentId, DocumentTabViewModel document, PanelFaceSourceShapeModel shape, string description) : IDocumentCommand, IExecutionTrackedCommand
    {
        private PanelFaceSourceShapeModel? _previous;
        public string Description => description;
        public Guid DocumentId => documentId;
        public bool WasExecuted { get; private set; }
        public void Execute()
        {
            var list = document.GetPanelFaceSourceShapes().ToArray();
            var index = Array.FindIndex(list, s => s.Id == shape.Id);
            if (document.DocumentId != documentId || index < 0) return;
            _previous = list[index];
            list[index] = shape;
            document.SetPanelFaceSourceShapes(list, new PanelChangeEvent(documentId, shape.Id, PanelChangeProperties.Geometry | PanelChangeProperties.Metadata, true, false, true, true));
            document.HierarchySelectedPanelSelection = ToSelection(shape);
            document.MarkDirty();
            WasExecuted = true;
        }
        public void Undo()
        {
            if (_previous is null) return;
            var list = document.GetPanelFaceSourceShapes().ToArray();
            var index = Array.FindIndex(list, s => s.Id == shape.Id);
            if (index < 0) return;
            list[index] = _previous;
            document.SetPanelFaceSourceShapes(list, new PanelChangeEvent(documentId, shape.Id, PanelChangeProperties.Geometry | PanelChangeProperties.Metadata, true, false, true, true));
        }
    }
}
