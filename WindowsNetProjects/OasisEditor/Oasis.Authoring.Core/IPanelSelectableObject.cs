namespace OasisEditor;

internal interface IPanelSelectableObject
{
    string ObjectId { get; }
    PanelElementKind ElementKind { get; }
    double X { get; }
    double Y { get; }
    double Width { get; }
    double Height { get; }
}
