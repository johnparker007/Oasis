namespace OasisEditor;

internal interface IMachineObjectReferenceResolver
{
    bool TryGetReference(PanelElementModel element, out MachineObjectReference reference);
    bool TryGetReference(InputDefinitionModel inputDefinition, out MachineObjectReference reference);
}

internal sealed class MachineObjectReferenceResolver : IMachineObjectReferenceResolver
{
    public static MachineObjectReferenceResolver Instance { get; } = new();

    private MachineObjectReferenceResolver()
    {
    }

    public bool TryGetReference(PanelElementModel element, out MachineObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(element);
        reference = MachineObjectReference.Empty;

        if (element.Kind == PanelElementKind.Alpha)
        {
            reference = MachineObjectReference.AlphaDisplay(element.DisplayNumber.GetValueOrDefault(0));
            return !reference.IsEmpty;
        }

        if (!element.DisplayNumber.HasValue)
        {
            return false;
        }

        reference = element.Kind switch
        {
            PanelElementKind.Lamp => MachineObjectReference.Lamp(element.DisplayNumber.Value),
            PanelElementKind.Reel => MachineObjectReference.Reel(element.DisplayNumber.Value),
            PanelElementKind.SevenSegment => MachineObjectReference.SevenSegmentDisplay(element.DisplayNumber.Value),
            _ => MachineObjectReference.Empty
        };

        return !reference.IsEmpty;
    }

    public bool TryGetReference(InputDefinitionModel inputDefinition, out MachineObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(inputDefinition);
        reference = MachineObjectReference.Input(inputDefinition.Id);
        return !reference.IsEmpty;
    }
}
