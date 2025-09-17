using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using LazyJedi.SevenZip;
using NUnit.Framework;
using UnityEngine;

namespace LazyExtractorTests
{
    public class LazyExtractorPersistentPathTests
    {
        [Test]
        public void Extract_WritesToPersistentDataPath_WhenOutPathIsRooted()
        {
            RunExtractionScenario((outputPath, archivePath) =>
            {
                LazyExtractor.Extract(outputPath, archivePath);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }

        [Test]
        public async Task ExtractAsync_WritesToPersistentDataPath_WhenOutPathIsRooted()
        {
            await RunExtractionScenario((outputPath, archivePath) => LazyExtractor.ExtractAsync(outputPath, archivePath));
        }

        private static async Task RunExtractionScenario(Func<string, string, Task> extractor)
        {
            string testId = Guid.NewGuid().ToString("N");
            string archivePath = Path.Combine(Application.temporaryCachePath, $"{testId}.zip");
            string extractPath = Path.Combine(Application.persistentDataPath, testId);
            string archiveEntry = $"subdir/{testId}.txt";
            string expectedExtractedFile = Path.Combine(extractPath, archiveEntry.Replace('/', Path.DirectorySeparatorChar));
            string projectRootFile = Path.Combine(Directory.GetCurrentDirectory(), archiveEntry.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                CreateZipArchive(archivePath, archiveEntry, "hello from lazy extractor");

                await extractor(extractPath, archivePath);

                Assert.That(File.Exists(expectedExtractedFile),
                    $"Expected extracted file at '{expectedExtractedFile}' was not found.");
                Assert.That(!File.Exists(projectRootFile),
                    $"File was unexpectedly created at project root: '{projectRootFile}'.");
            }
            finally
            {
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }
            }
        }

        private static void CreateZipArchive(string archivePath, string entryPath, string content)
        {
            using FileStream archiveStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write);
            using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry(entryPath);
            using Stream entryStream = entry.Open();
            using StreamWriter writer = new StreamWriter(entryStream);
            writer.Write(content);
        }
    }
}
