namespace OasisEditor;

public sealed class AmberBridgeException : Exception
{
    internal AmberBridgeException(string operation, AmberResult result, string? bridgeError = null)
        : base(BuildMessage(operation, result, bridgeError))
    {
        Operation = operation;
        ResultCode = (int)result;
        ResultName = result.ToString();
        BridgeError = bridgeError;
    }

    public string Operation { get; }
    public int ResultCode { get; }
    public string ResultName { get; }
    public string? BridgeError { get; }

    private static string BuildMessage(string operation, AmberResult result, string? error) =>
        string.IsNullOrWhiteSpace(error)
            ? $"Amber Bridge operation '{operation}' failed with {result} ({(int)result})."
            : $"Amber Bridge operation '{operation}' failed with {result} ({(int)result}): {error}";
}
