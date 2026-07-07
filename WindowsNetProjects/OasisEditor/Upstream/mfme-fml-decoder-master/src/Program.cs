using System;
using System.Net;

namespace MfmeFmlDecoder.Application
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var application = new Application();
            return application.Run(args);
        }
    }
}
