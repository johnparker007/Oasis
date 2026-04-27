using System;
using System.IO;
using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeExtractReaderTests
{
    [Fact]
    public void Read_WhenExtractPathMissing_ReturnsError()
    {
        using var temp = new TempPaths();
        var reader = new MfmeExtractReader();

        var result = reader.Read(new MfmeImportContext
        {
            SourceExtractPath = Path.Combine(temp.Root, "missing.extract"),
            ProjectRootPath = temp.ProjectRoot,
            AssetsRootPath = temp.AssetsRoot,
            CopyAssetsToProject = true
        });

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, error => error.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WhenManifestMissing_ReturnsError()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, error => error.Contains("manifest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_WhenComponentsMissing_ReturnsWarningWithoutErrors()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "layout.json"), """
            {
              "ASName": "Demo Layout"
            }
            """);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.False(result.HasErrors);
        Assert.NotNull(result.ExtractDocument);
        Assert.Equal("Demo Layout", result.ExtractDocument!.LayoutName);
        Assert.Contains(result.Warnings, warning => warning.Code == "missing-components");
    }

    [Fact]
    public void Read_WhenComponentHasNoType_SkipsWithWarning()
    {
        using var temp = new TempPaths();
        Directory.CreateDirectory(temp.ExtractRoot);
        File.WriteAllText(Path.Combine(temp.ExtractRoot, "layout.json"), """
            {
              "ASName": "Demo Layout",
              "Components": [
                {
                  "Position": { "X": 10, "Y": 20 },
                  "Size": { "X": 100, "Y": 40 }
                }
              ]
            }
            """);

        var reader = new MfmeExtractReader();
        var result = reader.Read(temp.CreateContext());

        Assert.False(result.HasErrors);
        Assert.Empty(result.ImportedElements);
        Assert.Single(result.SkippedComponents);
        Assert.Contains(result.Warnings, warning => warning.Code == "unsupported-component");
    }

    private sealed class TempPaths : IDisposable
    {
        public TempPaths()
        {
            Root = Path.Combine(Path.GetTempPath(), $"oasis-mfme-tests-{Guid.NewGuid():N}");
            ProjectRoot = Path.Combine(Root, "Project");
            AssetsRoot = Path.Combine(ProjectRoot, "Assets");
            ExtractRoot = Path.Combine(Root, "sample.extract");

            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(AssetsRoot);
        }

        public string Root { get; }
        public string ProjectRoot { get; }
        public string AssetsRoot { get; }
        public string ExtractRoot { get; }

        public MfmeImportContext CreateContext()
        {
            return new MfmeImportContext
            {
                SourceExtractPath = ExtractRoot,
                ProjectRootPath = ProjectRoot,
                AssetsRootPath = AssetsRoot,
                CopyAssetsToProject = true,
                LayoutDisplayName = null
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
