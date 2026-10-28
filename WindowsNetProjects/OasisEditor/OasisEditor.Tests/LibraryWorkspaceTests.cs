using OasisEditor.Authoring.Assets;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class LibraryWorkspaceTests
{
    [Fact]
    public async Task SharedReelCopyEditAssignmentAndWorkspaceRestoreUsePortableScopedReferences()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisLibraryWorkspace-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var session=new AuthoringSession(root);session.CreateProject("Project");using var capture=new WorkspaceCapture(session);var view=capture.View;
                var library=Path.Combine(root,"Library");Directory.CreateDirectory(library);view.SetSceneLibraryRoot(library);
                var projectManifest=view.Reels.Create("Physical");view.Reels.Update(widthMm:70);view.Reels.Save();
                var copied=view.CopyProjectAssetToLibrary(projectManifest);Assert.True(copied.Success,copied.Error);
                await view.RefreshLibraryAsync();Assert.Single(view.LibraryCatalog.Assets);
                Assert.False(view.CopyProjectAssetToLibrary(projectManifest).Success);
                await view.OpenLibraryAssetAsync(copied.Reference!.Path);Assert.True(view.Reels.IsLibraryAsset);
                var id=view.Reels.Document.Id;view.Reels.Update(widthMm:80);
                var another=Path.Combine(root,"Other Library");Directory.CreateDirectory(another);
                Assert.Throws<InvalidOperationException>(()=>view.SetSceneLibraryRoot(another));view.Reels.Save();
                Assert.True(ReelDocumentStorage.TryRead(File.ReadAllText(projectManifest),out var original,out _));Assert.Equal(70,original.WidthMm);
                var saved=view.InspectWorkspaceState();var libraryDoc=saved.AssetLayout!.Documents.Single(d=>d.LibraryRoot==library);Assert.Equal(copied.Reference.Path,libraryDoc.Path);
                view.ShowPanel();view.ApplyWorkspaceState(WorkspaceLayoutState.Deserialize(WorkspaceLayoutState.Serialize(saved)));await view.WaitForSceneAsync();
                Assert.Equal(id,view.Reels.Document.Id);Assert.Equal(80,view.Reels.Document.WidthMm);Assert.True(view.Reels.IsLibraryAsset);
                var imported=view.ImportLibraryAsset(copied.Reference.Path,"Imported");Assert.True(imported.Success,imported.Error);
                Assert.True(ReelDocumentStorage.TryRead(File.ReadAllText(imported.ManifestPath!),out var importedReel,out _));Assert.Equal(id,importedReel.Id);
                view.Machines.OpenMachine(view.Machines.ListMachines().Single().ManifestPath);view.AssignMachineReel(MachineObjectReference.Reel(0),copied.Reference);
                Assert.Equal(copied.Reference,Assert.Single(view.Machines.Document!.ReelAssignments).ReelAsset);
                view.Machines.Undo();Assert.Empty(view.Machines.Document!.ReelAssignments);view.Machines.Redo();
                var missing=AssetReference.Library("Reels/Missing/asset.reel");
                Assert.Contains(view.GetReusableAssetChoices(EditorAssetType.Reel,missing),choice=>choice.Reference==missing&&choice.Label.StartsWith("Missing:"));
                Assert.Contains(view.GetReusableAssetChoices(EditorAssetType.Reel),choice=>choice.Reference?.Scope==AssetReferenceScope.Project);
                Assert.Contains(view.GetReusableAssetChoices(EditorAssetType.Reel),choice=>choice.Reference==copied.Reference);
                Assert.Throws<ArgumentException>(()=>view.AssignMachineReel(MachineObjectReference.Lamp(0),copied.Reference));
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
