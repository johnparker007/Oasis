using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

internal static class CabinetMutationCommands
{
    public static Commands.ICommand CreateUpdateReflectionCommand(Guid documentId, DocumentTabViewModel document, string originalId, CabinetReflectionDefinition definition)
    {
        var current = document.GetCabinetDocument(); var reflections = current.Reflections ?? [];
        return new SetCabinetDocumentCommand(documentId, document, current with { Reflections = reflections.Select(item => item.Id == originalId ? definition : item).ToArray() }, "Update reflection receiver");
    }
    public static Commands.ICommand CreateAddReflectionCommand(Guid documentId, DocumentTabViewModel document, CabinetReflectionDefinition definition) { var current = document.GetCabinetDocument(); return new SetCabinetDocumentCommand(documentId, document, current with { Reflections = (current.Reflections ?? []).Append(definition).ToArray() }, "Add reflection receiver"); }
    public static Commands.ICommand CreateDeleteReflectionCommand(Guid documentId, DocumentTabViewModel document, string id) { var current = document.GetCabinetDocument(); return new SetCabinetDocumentCommand(documentId, document, current with { Reflections = (current.Reflections ?? []).Where(item => item.Id != id).ToArray() }, "Delete reflection receiver"); }
    public static Commands.ICommand CreateSetTargetFrontSideCommand(Guid documentId, DocumentTabViewModel document, string targetId, string frontSide)
    {
        return new SetCabinetSurfaceTargetSettingsCommand(documentId, document, targetId, CabinetSurfaceTargetSettings.NormalizeFrontSide(frontSide), null, null, "Set cabinet target front side");
    }

    public static Commands.ICommand CreateSetTargetFaceRotationCommand(Guid documentId, DocumentTabViewModel document, string targetId, int faceRotation)
    {
        return new SetCabinetSurfaceTargetSettingsCommand(documentId, document, targetId, null, CabinetSurfaceTargetSettings.NormalizeFaceRotation(faceRotation), null, "Set cabinet target face rotation");
    }

    public static Commands.ICommand CreateSetTargetFaceFlipHorizontalCommand(Guid documentId, DocumentTabViewModel document, string targetId, bool faceFlipHorizontal)
    {
        return new SetCabinetSurfaceTargetSettingsCommand(documentId, document, targetId, null, null, faceFlipHorizontal, "Set cabinet target horizontal flip");
    }


    private sealed class SetCabinetDocumentCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly CabinetDocument _nextDocument;
        private readonly string _description;
        private CabinetDocument? _originalDocument;

        public SetCabinetDocumentCommand(Guid documentId, DocumentTabViewModel document, CabinetDocument nextDocument, string description)
        {
            _documentId = documentId;
            _document = document;
            _nextDocument = nextDocument;
            _description = description;
        }

        public Guid DocumentId => _documentId;
        public string Description => _description;
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            _originalDocument ??= _document.GetCabinetDocument();
            _document.SetCabinetDocument(_nextDocument);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_originalDocument is null) return;
            _document.SetCabinetDocument(_originalDocument);
            _document.MarkDirty();
        }
    }

    private sealed class SetCabinetSurfaceTargetSettingsCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string _targetId;
        private readonly string? _frontSide;
        private readonly int? _faceRotation;
        private readonly bool? _faceFlipHorizontal;
        private readonly string _description;
        private CabinetDocument? _originalDocument;

        public SetCabinetSurfaceTargetSettingsCommand(Guid documentId, DocumentTabViewModel document, string targetId, string? frontSide, int? faceRotation, bool? faceFlipHorizontal, string description)
        {
            _documentId = documentId;
            _document = document;
            _targetId = targetId.Trim();
            _frontSide = frontSide;
            _faceRotation = faceRotation;
            _faceFlipHorizontal = faceFlipHorizontal;
            _description = description;
        }

        public Guid DocumentId => _documentId;
        public string Description => _description;
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            if (string.IsNullOrWhiteSpace(_targetId))
            {
                return;
            }

            var current = _document.GetCabinetDocument();
            var currentOverride = current.GetSurfaceTargetSettings(_targetId);
            var nextOverride = new CabinetSurfaceTargetSettings(
                _targetId,
                _frontSide ?? currentOverride.FrontSide,
                _faceRotation ?? currentOverride.FaceRotation,
                _faceFlipHorizontal ?? currentOverride.FaceFlipHorizontal).Normalized();

            if (string.Equals(currentOverride.FrontSide, nextOverride.FrontSide, StringComparison.OrdinalIgnoreCase)
                && currentOverride.FaceRotation == nextOverride.FaceRotation
                && currentOverride.FaceFlipHorizontal == nextOverride.FaceFlipHorizontal)
            {
                return;
            }

            _originalDocument ??= current;
            _document.SetCabinetDocument(current.WithSurfaceTargetSettings(nextOverride));
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_originalDocument is null)
            {
                return;
            }

            _document.SetCabinetDocument(_originalDocument);
            _document.MarkDirty();
        }
    }
}
