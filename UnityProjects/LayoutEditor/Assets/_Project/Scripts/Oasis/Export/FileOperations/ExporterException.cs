using System;

namespace Oasis.FileOperations
{
    public class FileSystemException : Exception
    {
        public FileSystemException(string message) : base(message)
        {
        }

        public FileSystemException(string message, System.Exception e) : base(message)
        {
        }
    }
}