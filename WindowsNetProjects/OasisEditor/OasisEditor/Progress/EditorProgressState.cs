namespace OasisEditor.Progress;

public sealed record EditorProgressState(
    string Title,
    string Message,
    EditorProgressMode Mode,
    double? Value,
    bool CanCancel,
    bool IsCancelling,
    string? ErrorMessage = null)
{
    public static EditorProgressState FromRequest(EditorProgressRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = request.Normalize();
        return new EditorProgressState(
            normalized.Title,
            normalized.InitialMessage,
            normalized.InitialMode,
            normalized.InitialMode == EditorProgressMode.Determinate ? 0d : null,
            normalized.CanCancel,
            IsCancelling: false);
    }

    public EditorProgressState WithDeterminateProgress(double progress, string message)
    {
        return this with
        {
            Mode = EditorProgressMode.Determinate,
            Value = Clamp(progress),
            Message = message?.Trim() ?? string.Empty,
            ErrorMessage = null
        };
    }

    public EditorProgressState WithIndeterminateMessage(string message)
    {
        return this with
        {
            Mode = EditorProgressMode.Indeterminate,
            Value = null,
            Message = message?.Trim() ?? string.Empty,
            ErrorMessage = null
        };
    }

    public EditorProgressState WithMessage(string message)
    {
        return this with
        {
            Message = message?.Trim() ?? string.Empty,
            ErrorMessage = null
        };
    }

    public EditorProgressState WithCancelling()
    {
        return this with { IsCancelling = true };
    }

    public EditorProgressState WithError(string errorMessage)
    {
        return this with { ErrorMessage = errorMessage?.Trim() ?? string.Empty };
    }

    public static double Clamp(double progress)
    {
        if (double.IsNaN(progress))
        {
            return 0d;
        }

        if (double.IsNegativeInfinity(progress))
        {
            return 0d;
        }

        if (double.IsPositiveInfinity(progress))
        {
            return 1d;
        }

        return Math.Clamp(progress, 0d, 1d);
    }
}
