using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor;

internal static class MachineMutationCommands
{
    public static Commands.ICommand CreateSetCabinetReferenceCommand(Guid documentId, DocumentTabViewModel document, string? cabinetAssetPath)
    {
        var current = document.GetMachineDocument();
        var normalized = ProjectAssetPathService.NormalizeAssetPackageDirectoryPath(cabinetAssetPath);
        return new SetMachineDocumentCommand(documentId, document, current with { CabinetAssetPath = normalized }, "Assign Cabinet to Machine");
    }

    public static Commands.ICommand CreateSetSurfaceAssignmentCommand(Guid documentId, DocumentTabViewModel document, string targetId, string? faceAssetPath)
    {
        var current = document.GetMachineDocument();
        var assignments = (current.SurfaceAssignments ?? []).Where(value => !string.Equals(value.TargetId, targetId, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(faceAssetPath))
        {
            assignments = assignments.Append(new MachineSurfaceAssignment(targetId, faceAssetPath).Normalized());
        }

        return new SetMachineDocumentCommand(documentId, document, current with { SurfaceAssignments = assignments.ToArray() }, "Assign Face to Machine surface");
    }

    public static Commands.ICommand CreateSetReelAssignmentCommand(Guid documentId, DocumentTabViewModel document, MachineObjectReference machineReelReference, string? reelSpecificationId)
    {
        var current = document.GetMachineDocument();
        var assignments = (current.ReelAssignments ?? []).Where(value => value.MachineReelReference != machineReelReference);
        if (!string.IsNullOrWhiteSpace(reelSpecificationId))
        {
            assignments = assignments.Append(new MachineReelAssignment(machineReelReference, reelSpecificationId).Normalized());
        }

        return new SetMachineDocumentCommand(documentId, document, current with { ReelAssignments = assignments.ToArray() }, "Assign reel specification to Machine reel");
    }

    public static Commands.ICommand CreateSetRuntimePlatformCommand(Guid documentId, DocumentTabViewModel document, FruitMachinePlatformType platform)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { Platform = platform } }, "Set Machine runtime platform");
    }

    public static Commands.ICommand CreateSetSystem6NativeRomSettingsCommand(Guid documentId, DocumentTabViewModel document, System6NativeRomSettings settings)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { System6NativeRoms = settings } }, "Update Machine System6 ROM settings");
    }

    public static Commands.ICommand CreateSetMpu5NativeRomSettingsCommand(Guid documentId, DocumentTabViewModel document, Mpu5NativeRomSettings settings)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { Mpu5NativeRoms = settings } }, "Update Machine MPU5 ROM settings");
    }

    public static Commands.ICommand CreateSetEpochNativeRomSettingsCommand(Guid documentId, DocumentTabViewModel document, EpochNativeRomSettings settings)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { EpochNativeRoms = settings } }, "Update Machine Epoch ROM settings");
    }

    public static Commands.ICommand CreateSetMpu3SettingsCommand(Guid documentId, DocumentTabViewModel document, Mpu3ProjectSettings settings)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { Mpu3Settings = settings } }, "Update Machine MPU3 settings");
    }

    public static Commands.ICommand CreateSetM1SettingsCommand(Guid documentId, DocumentTabViewModel document, M1ProjectSettings settings)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { M1Settings = settings } }, "Update Machine M1 settings");
    }

    public static Commands.ICommand CreateSetScorpion4SettingsCommand(Guid documentId, DocumentTabViewModel document, Scorpion4ProjectSettings settings)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { Runtime = current.Runtime with { Scorpion4Settings = settings } }, "Update Machine Scorpion4 settings");
    }

    public static Commands.ICommand CreateSetInputDefinitionsCommand(Guid documentId, DocumentTabViewModel document, InputDefinitionModel[] inputDefinitions)
    {
        var current = document.GetMachineDocument();
        return new SetMachineDocumentCommand(documentId, document, current with { InputDefinitions = inputDefinitions ?? [] }, "Update Machine input definitions");
    }

    private sealed class SetMachineDocumentCommand : Commands.IDocumentCommand, Commands.IExecutionTrackedCommand
    {
        private readonly Guid _documentId;
        private readonly DocumentTabViewModel _document;
        private readonly MachineDocument _nextDocument;
        private readonly string _description;
        private MachineDocument? _originalDocument;

        public SetMachineDocumentCommand(Guid documentId, DocumentTabViewModel document, MachineDocument nextDocument, string description)
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
            _originalDocument ??= _document.GetMachineDocument();
            _document.SetMachineDocument(_nextDocument);
            _document.MarkDirty();
            WasExecuted = true;
        }

        public void Undo()
        {
            if (_originalDocument is null)
            {
                return;
            }

            _document.SetMachineDocument(_originalDocument);
            _document.MarkDirty();
        }
    }
}
