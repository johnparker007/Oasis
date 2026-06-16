using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed class NativeCoreExportException : Exception
{
    public NativeCoreExportException(string message)
        : base(message)
    {
    }

    public NativeCoreExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static NativeCoreExportException CreateLoadFailure(string libraryPath, Architecture processArchitecture, Exception innerException)
    {
        return new NativeCoreExportException(
            $"Failed to load native core library '{libraryPath}'. Process architecture is {processArchitecture}; the native DLL must match the Oasis Editor process bitness. {innerException.Message}",
            innerException);
    }

    public static NativeCoreExportException CreateMissingExport(string libraryPath, Architecture processArchitecture, string exportName, Exception innerException)
    {
        return new NativeCoreExportException(
            $"Failed to bind native core export '{exportName}' from '{libraryPath}'. Process architecture is {processArchitecture}; verify the DLL is the expected core version and bitness. {innerException.Message}",
            innerException);
    }

    public static NativeCoreExportException CreateInvalidExportBinding(string libraryPath, Architecture processArchitecture, string exportName, Type delegateType, Exception innerException)
    {
        return new NativeCoreExportException(
            $"Failed to bind native core export '{exportName}' from '{libraryPath}' to delegate '{delegateType.FullName}'. Process architecture is {processArchitecture}; verify the managed delegate signature and native calling convention match. {innerException.Message}",
            innerException);
    }
}
