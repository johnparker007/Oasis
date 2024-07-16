using System;

namespace Oasis.Export
{
    public class ExporterException : Exception
    {
        public ExporterException(string message) : base(message)
        {
        }

        public ExporterException(string message, System.Exception e) : base(message)
        {
        }
    }
}