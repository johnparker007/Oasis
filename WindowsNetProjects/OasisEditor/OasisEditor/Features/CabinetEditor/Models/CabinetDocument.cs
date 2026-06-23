namespace OasisEditor.Features.CabinetEditor.Models;

public sealed record CabinetDocument(CabinetModelReference Model)
{
    public static CabinetDocument FromModelPath(string modelPath) => new(new CabinetModelReference(modelPath, 1.0, "Y"));
}
