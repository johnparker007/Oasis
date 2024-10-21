using System;

namespace Oasis.Import
{
    public class ImportParseException : Exception
    {
        public ImportParseException(string message) : base(message)
        {
        }

        public ImportParseException(string message, System.Exception e) : base(message)
        {
        }
    }
}