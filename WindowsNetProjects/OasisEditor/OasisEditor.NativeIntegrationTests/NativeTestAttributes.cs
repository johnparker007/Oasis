using Xunit;

namespace OasisEditor.NativeIntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NativeFactAttribute : FactAttribute
{
    public NativeFactAttribute(params string[] prerequisites)
    {
        Skip = NativePrerequisites.GetSkipReason(prerequisites);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NativeTheoryAttribute : TheoryAttribute
{
    public NativeTheoryAttribute(params string[] prerequisites)
    {
        Skip = NativePrerequisites.GetSkipReason(prerequisites);
    }
}
