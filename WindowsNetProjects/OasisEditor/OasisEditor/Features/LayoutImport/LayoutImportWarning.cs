namespace OasisEditor.Features.LayoutImport;

internal sealed record LayoutImportWarning(string Code, string Message, string? Context = null);
