using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor.Features.MachineEditor.ViewModels;

public sealed class MachineDocumentViewModel : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _document;
    private readonly Func<EditorProject?> _projectAccessor;
    private readonly Func<IReadOnlyList<DocumentTabViewModel>> _openDocumentsAccessor;
    private string? _selectedCabinetAssetPath;
    private bool _isRefreshingSelections;

    public MachineDocumentViewModel(
        DocumentTabViewModel document,
        Func<EditorProject?> projectAccessor,
        Func<IReadOnlyList<DocumentTabViewModel>> openDocumentsAccessor)
    {
        _document = document;
        _projectAccessor = projectAccessor;
        _openDocumentsAccessor = openDocumentsAccessor;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> CabinetAssetChoices { get; } = new();
    public ObservableCollection<MachineSurfaceAssignmentRowViewModel> SurfaceAssignments { get; } = new();
    public ObservableCollection<MachineReelAssignmentRowViewModel> ReelAssignments { get; } = new();

    public string? SelectedCabinetAssetPath
    {
        get => _selectedCabinetAssetPath;
        set
        {
            var normalized = ProjectAssetPathService.NormalizeAssetPackageDirectoryPath(value);
            if (string.Equals(_selectedCabinetAssetPath, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_isRefreshingSelections)
            {
                return;
            }

            _selectedCabinetAssetPath = normalized;

            _document.CommandService.Execute(MachineMutationCommands.CreateSetCabinetReferenceCommand(_document.DocumentId, _document, normalized));
            Refresh();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCabinetAssetPath)));
        }
    }

    public FruitMachinePlatformType SelectedPlatform
    {
        get => _document.GetMachineDocument().Runtime.Platform;
        set
        {
            if (_document.GetMachineDocument().Runtime.Platform == value)
            {
                return;
            }

            _document.CommandService.Execute(MachineMutationCommands.CreateSetRuntimePlatformCommand(_document.DocumentId, _document, value));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPlatform)));
        }
    }

    public IReadOnlyList<FruitMachinePlatformType> PlatformChoices { get; } = Enum.GetValues<FruitMachinePlatformType>();

    public void Refresh()
    {
        _isRefreshingSelections = true;
        try
        {
            RefreshCore();
        }
        finally
        {
            _isRefreshingSelections = false;
        }
    }

    private void RefreshCore()
    {
        var machine = _document.GetMachineDocument();
        var normalizedSelection = NormalizeCabinetAssetPath(machine.CabinetAssetPath, _projectAccessor);
        var choices = new List<string>();
        var seenChoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var choice in DiscoverCabinetAssets())
        {
            var normalizedChoice = NormalizeCabinetAssetPath(choice, _projectAccessor);
            if (string.IsNullOrWhiteSpace(normalizedChoice) || !seenChoices.Add(normalizedChoice))
            {
                continue;
            }

            choices.Add(normalizedChoice);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSelection) && seenChoices.Add(normalizedSelection))
        {
            choices.Add(normalizedSelection);
        }

        CabinetAssetChoices.Clear();
        foreach (var choice in choices.OrderBy(choice => choice, StringComparer.OrdinalIgnoreCase))
        {
            CabinetAssetChoices.Add(choice);
        }

        _selectedCabinetAssetPath = string.IsNullOrWhiteSpace(normalizedSelection)
            ? null
            : choices.FirstOrDefault(choice => string.Equals(choice, normalizedSelection, StringComparison.OrdinalIgnoreCase)) ?? normalizedSelection;

        SurfaceAssignments.Clear();
        foreach (var target in DiscoverCabinetTargets(machine.CabinetAssetPath))
        {
            var assignment = machine.SurfaceAssignments?.FirstOrDefault(value => string.Equals(value.TargetId, target.Id, StringComparison.Ordinal));
            SurfaceAssignments.Add(new MachineSurfaceAssignmentRowViewModel(_document, target, assignment?.FaceAssetPath, DiscoverFaceAssets()));
        }

        ReelAssignments.Clear();
        var specifications = ResolveCabinetDocument(machine.CabinetAssetPath)?.ReelSpecifications ?? [];
        var specChoices = specifications.Select(spec => spec.Id).Prepend("(None)").ToArray();
        foreach (var reelIndex in Enumerable.Range(0, 4))
        {
            var reference = MachineObjectReference.Reel(reelIndex);
            var assignment = machine.ReelAssignments?.FirstOrDefault(value => value.MachineReelReference == reference);
            ReelAssignments.Add(new MachineReelAssignmentRowViewModel(_document, reelIndex, assignment?.ReelSpecificationId, specChoices));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCabinetAssetPath)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPlatform)));
    }

    private IEnumerable<string> DiscoverCabinetAssets()
    {
        var project = _projectAccessor();
        if (project is null)
        {
            yield break;
        }

        var root = Path.Combine(project.AssetsDirectory, "Cabinet3D");
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var manifest in Directory.EnumerateFiles(root, ProjectAssetPathService.Cabinet3DManifestFileName, SearchOption.AllDirectories))
        {
            var relative = new ProjectAssetPathService().ToProjectRelativePath(project, manifest);
            var packageDirectory = ProjectAssetPathService.NormalizeAssetPackageDirectoryPath(Path.GetDirectoryName(relative));
            if (!string.IsNullOrWhiteSpace(packageDirectory))
            {
                yield return packageDirectory;
            }
        }
    }

    private IEnumerable<CabinetFaceTarget> DiscoverCabinetTargets(string? cabinetAssetPath)
    {
        var cabinet = ResolveCabinetDocument(cabinetAssetPath);
        if (cabinet is null || string.IsNullOrWhiteSpace(cabinet.Model.Path) || !File.Exists(cabinet.Model.Path))
        {
            return [];
        }

        try
        {
            return new GlbCabinetFaceTargetDetector().DetectTargets(cabinet.Model.Path).Where(target => target.IsValid);
        }
        catch (EndOfStreamException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private CabinetDocument? ResolveCabinetDocument(string? cabinetAssetPath)
    {
        if (string.IsNullOrWhiteSpace(cabinetAssetPath))
        {
            return null;
        }

        var normalizedCabinetAssetPath = NormalizeCabinetAssetPath(cabinetAssetPath);
        var open = _openDocumentsAccessor()
            .FirstOrDefault(document => document.Document.DocumentType == EditorDocumentType.Cabinet3D
                && string.Equals(NormalizeCabinetAssetPath(document.FilePath, _projectAccessor), normalizedCabinetAssetPath, StringComparison.OrdinalIgnoreCase));
        if (open is not null)
        {
            return open.GetCabinetDocument();
        }

        var project = _projectAccessor();
        if (project is null)
        {
            return null;
        }

        var manifestPath = Path.IsPathRooted(cabinetAssetPath)
            ? cabinetAssetPath
            : Path.Combine(project.ProjectDirectory, cabinetAssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(manifestPath))
        {
            manifestPath = Path.Combine(manifestPath, ProjectAssetPathService.Cabinet3DManifestFileName);
        }

        return File.Exists(manifestPath) && CabinetDocumentStorage.TryRead(File.ReadAllText(manifestPath), out var document)
            ? document
            : null;
    }

    private IEnumerable<string> DiscoverFaceAssets()
    {
        var project = _projectAccessor();
        if (project is null)
        {
            yield break;
        }

        var root = Path.Combine(project.AssetsDirectory, "Faces");
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var manifest in Directory.EnumerateFiles(root, ProjectAssetPathService.FaceManifestFileName, SearchOption.AllDirectories))
        {
            yield return new ProjectAssetPathService().ToProjectRelativePath(project, Path.GetDirectoryName(manifest)!);
        }
    }

    private static string NormalizeCabinetAssetPath(string? path, Func<EditorProject?>? projectAccessor = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var project = projectAccessor?.Invoke();
        var fullPath = path.Trim();
        if (project is not null && !Path.IsPathRooted(fullPath))
        {
            fullPath = Path.Combine(project.ProjectDirectory, fullPath.Replace('/', Path.DirectorySeparatorChar));
        }

        if (Path.IsPathRooted(fullPath))
        {
            fullPath = Path.GetFullPath(fullPath);
            if (File.Exists(fullPath) && string.Equals(Path.GetFileName(fullPath), ProjectAssetPathService.Cabinet3DManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = Path.GetDirectoryName(fullPath)!;
            }

            if (project is not null)
            {
                return ProjectAssetPathService.NormalizeAssetPackageDirectoryPath(
                    ProjectAssetPathService.NormalizeProjectRelativePath(Path.GetRelativePath(project.ProjectDirectory, fullPath))) ?? string.Empty;
            }
        }

        return ProjectAssetPathService.NormalizeAssetPackageDirectoryPath(fullPath) ?? string.Empty;
    }
}

public sealed class MachineSurfaceAssignmentRowViewModel
{
    private readonly DocumentTabViewModel _document;
    private string _selectedFaceAssetPath;

    public MachineSurfaceAssignmentRowViewModel(DocumentTabViewModel document, CabinetFaceTarget target, string? assignedFaceAssetPath, IEnumerable<string> faceChoices)
    {
        _document = document;
        TargetId = target.Id;
        TargetLabel = target.DisplayName;
        FaceChoices = faceChoices.Prepend("(None)").ToArray();
        _selectedFaceAssetPath = string.IsNullOrWhiteSpace(assignedFaceAssetPath) ? "(None)" : assignedFaceAssetPath;
    }

    public string TargetId { get; }
    public string TargetLabel { get; }
    public IReadOnlyList<string> FaceChoices { get; }
    public string SelectedFaceAssetPath
    {
        get => _selectedFaceAssetPath;
        set
        {
            if (string.Equals(_selectedFaceAssetPath, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedFaceAssetPath = value;
            _document.CommandService.Execute(MachineMutationCommands.CreateSetSurfaceAssignmentCommand(
                _document.DocumentId,
                _document,
                TargetId,
                string.Equals(value, "(None)", StringComparison.Ordinal) ? null : value));
        }
    }
}

public sealed class MachineReelAssignmentRowViewModel
{
    private readonly DocumentTabViewModel _document;
    private readonly int _reelIndex;
    private string _selectedSpecificationId;

    public MachineReelAssignmentRowViewModel(DocumentTabViewModel document, int reelIndex, string? specificationId, IReadOnlyList<string> specificationChoices)
    {
        _document = document;
        _reelIndex = reelIndex;
        Label = $"Reel {_reelIndex}";
        SpecificationChoices = specificationChoices;
        _selectedSpecificationId = string.IsNullOrWhiteSpace(specificationId) ? "(None)" : specificationId;
    }

    public string Label { get; }
    public IReadOnlyList<string> SpecificationChoices { get; }
    public string SelectedSpecificationId
    {
        get => _selectedSpecificationId;
        set
        {
            if (string.Equals(_selectedSpecificationId, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSpecificationId = value;
            _document.CommandService.Execute(MachineMutationCommands.CreateSetReelAssignmentCommand(
                _document.DocumentId,
                _document,
                MachineObjectReference.Reel(_reelIndex),
                string.Equals(value, "(None)", StringComparison.Ordinal) ? null : value));
        }
    }
}
