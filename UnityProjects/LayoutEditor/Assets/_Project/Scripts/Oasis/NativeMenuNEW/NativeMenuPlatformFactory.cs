using System;

namespace Oasis.NativeMenuNEW
{
    internal interface INativeMenuPlatform : IDisposable
    {
        void Initialize(NativeMenuManager manager);
    }

    internal sealed class NativeMenuNoOpPlatform : INativeMenuPlatform
    {
        public void Initialize(NativeMenuManager manager)
        {
        }

        public void Dispose()
        {
        }
    }

    internal static class NativeMenuPlatformFactory
    {
        public static INativeMenuPlatform Create()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return new NativeMenuWindowsPlatform();
#else
            return new NativeMenuNoOpPlatform();
#endif
        }
    }
}
