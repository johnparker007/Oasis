using System;
using System.IO;
using System.IO.Compression;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;

namespace MfmeFmlDecoder.Utilities
{
    internal static class LayoutExporter
    {
        public static void ExportToZip(Layout layout, string outputZipPath)
        {
            if (layout is null) throw new ArgumentNullException(nameof(layout));
            if (string.IsNullOrWhiteSpace(outputZipPath)) throw new ArgumentException("Output zip path is required.", nameof(outputZipPath));

            string tempDir = Path.Combine(Path.GetTempPath(), "mfme-fml-decoder-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                WriteLayoutJson(layout, tempDir);
                WriteComponentImages(layout, tempDir);
                WriteZipArchive(tempDir, outputZipPath);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                }
            }
        }

        private static void WriteLayoutJson(Layout layout, string tempDir)
        {
            string jsonPath = Path.Combine(tempDir, "layout.json");
            File.WriteAllText(jsonPath, layout.ToJson(indented: true));
        }

        private static void WriteComponentImages(Layout layout, string tempDir)
        {
            for (int zOrder = 0; zOrder < layout.Components.Count; zOrder++)
            {
                BaseComponent component = layout.Components[zOrder];
                if (component.Images.Count == 0)
                {
                    continue;
                }

                string imageDir = Path.Combine(tempDir, "images", zOrder.ToString());
                Directory.CreateDirectory(imageDir);

                foreach (var kvp in component.Images)
                {
                    string fileName = kvp.Key + ".bmp";
                    File.WriteAllBytes(Path.Combine(imageDir, fileName), kvp.Value.Bytes);
                }
            }
        }

        private static void WriteZipArchive(string sourceDir, string outputZipPath)
        {
            string fullOutputPath = Path.GetFullPath(outputZipPath);
            string outputDir = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string tempZipPath = Path.Combine(Path.GetTempPath(), "mfme-fml-decoder-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }

                ZipFile.CreateFromDirectory(sourceDir, tempZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                File.Copy(tempZipPath, fullOutputPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
