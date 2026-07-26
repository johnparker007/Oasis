namespace OasisEditor.Features.CabinetEditor.Models;

public sealed record CabinetReflectionMaterialSlot(int Index, string MaterialName, string ShaderType)
{
    public string DisplayName => $"Material {Index}: {MaterialName}";
}

public sealed record CabinetReflectionReceiverTarget(string TargetPath, string Name, string MeshName, IReadOnlyList<CabinetReflectionMaterialSlot> MaterialSlots)
{
    public string DisplayName => $"{TargetPath} ({MeshName}, {MaterialSlots.Count} material slot{(MaterialSlots.Count == 1 ? string.Empty : "s")})";
}
