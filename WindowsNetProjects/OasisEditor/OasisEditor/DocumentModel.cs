namespace OasisEditor;

public enum EditorDocumentType
{
    Generic,
    ProjectOverview,
    Panel2D,
    Cabinet3D,
    Machine
}

public sealed class EditorDocument
{
    private EditorDocument(
        string title,
        EditorDocumentType documentType,
        string filePath,
        string contentSummary,
        bool isUntitled)
    {
        Title = title;
        DocumentType = documentType;
        FilePath = filePath;
        ContentSummary = contentSummary;
        IsUntitled = isUntitled;
    }

    public string Title { get; }
    public EditorDocumentType DocumentType { get; }
    public string FilePath { get; }
    public string ContentSummary { get; }
    public bool IsUntitled { get; }

    public static EditorDocument CreateUntitled(string title)
    {
        return new EditorDocument(
            title,
            EditorDocumentType.Generic,
            "No file associated yet.",
            "Create or open a project asset to begin editing.",
            true);
    }

    public static EditorDocument CreateProjectOverview(EditorProject project)
    {
        return new EditorDocument(
            "Project Overview",
            EditorDocumentType.ProjectOverview,
            project.ProjectFilePath,
            $"Assets: {project.AssetsDirectory}\nMachines: {project.MachinesDirectory}\nGenerated: {project.GeneratedDirectory}",
            false);
    }

    public static EditorDocument CreateFromFile(string filePath, string summary)
    {
        var extension = System.IO.Path.GetExtension(filePath);
        var normalizedExtension = extension.ToLowerInvariant();

        EditorDocumentType documentType;
        if (normalizedExtension == ".panel2d")
        {
            documentType = EditorDocumentType.Panel2D;
        }
        else if (normalizedExtension == ".cabinet3d")
        {
            documentType = EditorDocumentType.Cabinet3D;
        }
        else if (normalizedExtension == ".machine")
        {
            documentType = EditorDocumentType.Machine;
        }
        else
        {
            documentType = EditorDocumentType.Generic;
        }

        return new EditorDocument(
            System.IO.Path.GetFileName(filePath),
            documentType,
            filePath,
            summary,
            false);
    }

    public EditorDocument SaveAs(string filePath, string summary)
    {
        return CreateFromFile(filePath, summary);
    }
}
