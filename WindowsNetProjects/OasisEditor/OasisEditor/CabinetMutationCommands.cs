using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

internal static class CabinetMutationCommands
{
    public static Commands.ICommand CreateSetTargetFrontSideCommand(Guid documentId, DocumentTabViewModel document, string targetId, string frontSide)
    {
        return new SetCabinetTargetOverrideCommand(documentId, document, targetId, CabinetTargetOverride.NormalizeFrontSide(frontSide), null, "Set cabinet target front side");
    }

    public static Commands.ICommand CreateSetTargetFaceRotationCommand(Guid documentId, DocumentTabViewModel document, string targetId, int faceRotation)
    {
        return new SetCabinetTargetOverrideCommand(documentId, document, targetId, null, CabinetTargetOverride.NormalizeFaceRotation(faceRotation), "Set cabinet target face rotation");
    }

    private sealed class SetCabinetTargetOverrideCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string _targetId;
        private readonly string? _frontSide;
        private readonly int? _faceRotation;
        private readonly string _description;
        private CabinetDocument? _originalDocument;

        public SetCabinetTargetOverrideCommand(Guid documentId, DocumentTabViewModel document, string targetId, string? frontSide, int? faceRotation, string description)
        {
            _documentId = documentId;
            _document = document;
            _targetId = targetId.Trim();
            _frontSide = frontSide;
            _faceRotation = faceRotation;
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
            var currentOverride = current.GetTargetOverride(_targetId);
            var nextOverride = new CabinetTargetOverride(
                _targetId,
                _frontSide ?? currentOverride.FrontSide,
                _faceRotation ?? currentOverride.FaceRotation).Normalized();

            if (string.Equals(currentOverride.FrontSide, nextOverride.FrontSide, StringComparison.OrdinalIgnoreCase)
                && currentOverride.FaceRotation == nextOverride.FaceRotation)
            {
                return;
            }

            _originalDocument ??= current;
            _document.SetCabinetDocument(current.WithTargetOverride(nextOverride));
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
