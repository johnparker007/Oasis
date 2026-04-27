namespace OasisEditor.Features.MfmeImport;

internal sealed record MfmeImportWarning(string Code, string Message, string? ContextPath = null)
{
    public string ToDisplayMessage()
    {
        return string.IsNullOrWhiteSpace(ContextPath)
            ? $"[{Code}] {Message}"
            : $"[{Code}] {Message} ({ContextPath})";
    }
}
