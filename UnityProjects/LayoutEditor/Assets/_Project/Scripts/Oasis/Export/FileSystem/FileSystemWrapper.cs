#nullable enable
using System.IO;

//TODO: We may move this to a more sensible namespace, but this will do for now.
namespace Oasis.Export
{
    /// <summary>
    /// A mockable wrapper around various file operations
    /// </summary>
    public class FileSystemWrapper
    {
        public void WriteAllText(string path, string createText)
        {
            try
            {
                File.WriteAllText(path, createText);
            }
            catch (IOException e) {
                throw new ExporterException(string.Format("unable to write file {0}", path), e);
            }
        }
    }
}