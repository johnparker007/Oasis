namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeImportWarning(string Code, string Message, string? Context = null);
