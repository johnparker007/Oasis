using System;
using System.Runtime.InteropServices;
using Xunit;

namespace OasisEditor.Tests;

public sealed class NativeCoreExportExceptionTests
{
    [Fact]
    public void CreateLoadFailureIncludesPathArchitectureAndOriginalError()
    {
        var inner = new DllNotFoundException("native dependency missing");

        var exception = NativeCoreExportException.CreateLoadFailure("C:/cores/system6.dll", Architecture.X64, inner);

        Assert.Contains("C:/cores/system6.dll", exception.Message);
        Assert.Contains("X64", exception.Message);
        Assert.Contains("native dependency missing", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void CreateMissingExportIncludesExportPathAndArchitecture()
    {
        var inner = new EntryPointNotFoundException("export missing");

        var exception = NativeCoreExportException.CreateMissingExport("C:/cores/system6.dll", Architecture.X86, "SYSTEM6Run", inner);

        Assert.Contains("SYSTEM6Run", exception.Message);
        Assert.Contains("C:/cores/system6.dll", exception.Message);
        Assert.Contains("X86", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void CreateInvalidExportBindingIncludesDelegateType()
    {
        var inner = new ArgumentException("invalid pointer");

        var exception = NativeCoreExportException.CreateInvalidExportBinding(
            "C:/cores/system6.dll",
            Architecture.X64,
            "SYSTEM6LoadROM",
            typeof(System6NativeLibrary.System6LoadRomDelegate),
            inner);

        Assert.Contains("SYSTEM6LoadROM", exception.Message);
        Assert.Contains(nameof(System6NativeLibrary.System6LoadRomDelegate), exception.Message);
        Assert.Contains("X64", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
