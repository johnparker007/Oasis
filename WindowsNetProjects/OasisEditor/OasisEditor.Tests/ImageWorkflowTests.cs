using SkiaSharp;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class ImageWorkflowTests
{
    [Fact]
    public void ImageImportCopiesDecodedPixelsAndParticipatesInDocumentHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisImageTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "Workspace.oasisproj");
            File.WriteAllText(file, """{"name":"Images","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            var source = Path.Combine(root, "Artwork.png");
            using (var bitmap = new SKBitmap(16, 24))
            { bitmap.Erase(SKColors.CornflowerBlue); using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100); using var stream = File.Create(source); data.SaveTo(stream); }
            var session = new AuthoringSession(root);
            session.OpenProject(file); session.CreatePanel("Artwork panel");
            var id = session.ImportImage(source);
            var element = Assert.Single(session.Elements);
            Assert.Equal(16, element.Width); Assert.Equal(24, element.Height);
            Assert.Equal(id, session.SelectedId);
            var imported = session.ResolveAsset(element.AssetPath!);
            Assert.NotEqual(source, imported);
            using (var bitmap = SKBitmap.Decode(imported)) Assert.Equal(SKColors.CornflowerBlue, bitmap.GetPixel(0, 0));
            session.Undo(); Assert.Empty(session.Elements); Assert.False(session.IsDirty);
            session.Redo(); Assert.Single(session.Elements); session.Save();
            session.OpenPanel(session.PanelPath!); Assert.Equal(id, Assert.Single(session.Elements).Id);
            Assert.Throws<InvalidDataException>(() => session.ImportImage(Path.Combine(root, "..", "outside.png")));
        }
        finally { Directory.Delete(root, true); }
    }
}
