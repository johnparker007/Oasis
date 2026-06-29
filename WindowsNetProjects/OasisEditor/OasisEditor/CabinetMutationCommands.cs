using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

internal static class CabinetMutationCommands
{
    public static Commands.ICommand CreateSetTargetFrontSideCommand(Guid documentId, DocumentTabViewModel document, string targetId, string frontSide)
    {
        return new SetCabinetTargetOverrideCommand(documentId, document, targetId, CabinetTargetOverride.NormalizeFrontSide(frontSide), null, null, "Set cabinet target front side");
    }

    public static Commands.ICommand CreateSetTargetFaceRotationCommand(Guid documentId, DocumentTabViewModel document, string targetId, int faceRotation)
    {
        return new SetCabinetTargetOverrideCommand(documentId, document, targetId, null, CabinetTargetOverride.NormalizeFaceRotation(faceRotation), null, "Set cabinet target face rotation");
    }

    public static Commands.ICommand CreateSetTargetFaceFlipHorizontalCommand(Guid documentId, DocumentTabViewModel document, string targetId, bool faceFlipHorizontal)
    {
        return new SetCabinetTargetOverrideCommand(documentId, document, targetId, null, null, faceFlipHorizontal, "Set cabinet target horizontal flip");
    }

    public static Commands.ICommand CreateSetPreviewLampModeCommand(Guid documentId, DocumentTabViewModel document, string lampPreviewMode)
    {
        return new SetCabinetPreviewLampModeCommand(documentId, document, CabinetLampPreviewMode.Normalize(lampPreviewMode));
    }

    private sealed class SetCabinetPreviewLampModeCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string _lampPreviewMode;
        private CabinetDocument? _originalDocument;

        public SetCabinetPreviewLampModeCommand(Guid documentId, DocumentTabViewModel document, string lampPreviewMode)
        {
            _documentId = documentId;
            _document = document;
            _lampPreviewMode = CabinetLampPreviewMode.Normalize(lampPreviewMode);
        }

        public Guid DocumentId => _documentId;
        public string Description => "Set cabinet lamp preview mode";
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = false;
            var current = _document.GetCabinetDocument();
            if (string.Equals(CabinetLampPreviewMode.Normalize(current.Preview.LampPreviewMode), _lampPreviewMode, StringComparison.Ordinal))
            {
                return;
            }

            _originalDocument ??= current;
            _document.SetCabinetDocument(current with { Preview = current.Preview with { LampPreviewMode = _lampPreviewMode } });
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

    private sealed class SetCabinetTargetOverrideCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly string _targetId;
        private readonly string? _frontSide;
        private readonly int? _faceRotation;
        private readonly bool? _faceFlipHorizontal;
        private readonly string _description;
        private CabinetDocument? _originalDocument;

        public SetCabinetTargetOverrideCommand(Guid documentId, DocumentTabViewModel document, string targetId, string? frontSide, int? faceRotation, bool? faceFlipHorizontal, string description)
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
            var currentOverride = current.GetTargetOverride(_targetId);
            var nextOverride = new CabinetTargetOverride(
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
