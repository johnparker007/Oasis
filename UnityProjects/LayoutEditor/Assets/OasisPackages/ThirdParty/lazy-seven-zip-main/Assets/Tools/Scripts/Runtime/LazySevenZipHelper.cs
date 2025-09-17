using SevenZip;

namespace LazyJedi.SevenZip
{
    public static class LazySevenZipHelper
    {
        public static string ArchiveFormatToExtension(OutArchiveFormat archiveFormat)
        {
            switch (archiveFormat)
            {
                case OutArchiveFormat.SevenZip:
                    return ".7z";
                case OutArchiveFormat.GZip:
                    return ".gz";
                case OutArchiveFormat.BZip2:
                    return ".bz2";
                case OutArchiveFormat.Tar:
                    return ".tar";
                case OutArchiveFormat.XZ:
                    return ".xz";
                case OutArchiveFormat.Zip:
                default:
                    return ".zip";
            }
        }

        public static OutArchiveFormat ArchiveExtensionToFormat(string extension)
        {
            switch (extension)
            {
                case ".7z":
                    return OutArchiveFormat.SevenZip;
                case ".gz":
                    return OutArchiveFormat.GZip;
                case ".bz2":
                    return OutArchiveFormat.BZip2;
                case ".tar":
                    return OutArchiveFormat.Tar;
                case ".xz":
                    return OutArchiveFormat.XZ;
                case ".zip":
                    return OutArchiveFormat.Zip;
                default:
                    return OutArchiveFormat.Zip;
            }
        }

        public static bool IsArchiveExtension(string extension)
        {
            return extension.Equals(".zip") || extension.Equals(".7z") || extension.Equals(".rar") || extension.Equals(".gz") || extension.Equals(".bz2") ||
                   extension.Equals(".tar") || extension.Equals(".xz");
        }
    }
}