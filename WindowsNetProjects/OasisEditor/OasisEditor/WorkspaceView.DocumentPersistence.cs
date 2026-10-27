namespace OasisEditor.Avalonia;

public sealed record WorkspaceDocumentState(string Kind,string Path,string? LibraryRoot=null);

public sealed partial class WorkspaceView
{
    private WorkspaceDocumentState[] CaptureDocumentList()=>InspectOpenDocuments().Select(d=>new WorkspaceDocumentState(d.Kind,
        Path.GetRelativePath(d.LibraryRoot??_session.RequireProjectDirectory(),d.Path).Replace('\\','/'),d.LibraryRoot)).ToArray();

    private static void ReadDocumentList(WorkspaceState state, AuthoringSession project, FaceAuthoringSession faces,
        MachineAuthoringSession machines,CabinetAuthoringSession cabinets,PrefabAuthoringSession prefabs,ReelAuthoringSession reels)
    {
        var docs=state.Documents??[];
        if(docs.Length>128)throw new InvalidDataException("Too many workspace documents.");
        var seen=new HashSet<string>(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        foreach(var d in docs)
        {
            if(d is null || d.Kind is not("Panel2D" or "Face" or "Machine" or "Cabinet3D" or "Prefab" or "Reel") || string.IsNullOrWhiteSpace(d.Path) || Path.IsPathRooted(d.Path))
                throw new InvalidDataException("Invalid workspace document.");
            if(!seen.Add(d.Kind+":"+(d.LibraryRoot??"")+":"+d.Path))throw new InvalidDataException("Duplicate workspace document.");
            if(d.LibraryRoot is not null)
            {
                WorkspacePathPolicy.RejectRootReparsePoints(Path.GetFullPath(d.LibraryRoot),requireExists:true);
                switch(d.Kind)
                {
                    case "Cabinet3D":cabinets.OpenLibrary(d.LibraryRoot,d.Path);break;
                    case "Prefab":prefabs.OpenLibrary(d.LibraryRoot,d.Path);break;
                    case "Reel":reels.OpenLibrary(d.LibraryRoot,d.Path);break;
                    default:throw new InvalidDataException("Unsupported Library document.");
                }
            }
            else switch(d.Kind)
            {
                case "Panel2D":project.OpenPanel(d.Path);break;
                case "Face":faces.OpenFace(d.Path);break;
                case "Machine":machines.OpenMachine(d.Path);break;
                case "Cabinet3D":cabinets.Open(d.Path);break;
                case "Prefab":prefabs.Open(d.Path);break;
                case "Reel":reels.Open(d.Path);break;
            }
        }
    }
    private void PreflightDocumentList(WorkspaceState state,AuthoringSession project)
        =>ReadDocumentList(state,project,new(project),new(project),new(project),new(project),new(project));
    private void RestoreDocumentList(WorkspaceState state)
        =>ReadDocumentList(state,_session,Faces,Machines,Cabinets,Prefabs,Reels);
}
