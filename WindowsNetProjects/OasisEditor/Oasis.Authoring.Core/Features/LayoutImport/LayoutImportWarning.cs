namespace OasisEditor.Features.LayoutImport;

public sealed record LayoutImportWarning(string Code, string Message, string? Context = null);
