using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Microsoft.Win32;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetReflectionEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentTabViewModel _document;
    private readonly Func<EditorProject?>? _projectAccessor;
    private readonly SynchronizationContext? _context;
    private FileSystemWatcher? _watcher;
    private Timer? _refreshDebounce;
    private readonly object _refreshGate = new();
    private bool _attached;
    private bool _disposed;
    private CabinetReflectionDefinition? _selected;
    private CabinetReflectionSourceViewModel? _selectedSource;

    public CabinetReflectionEditorViewModel(DocumentTabViewModel document, Func<EditorProject?>? projectAccessor = null)
    {
        _document = document; _projectAccessor = projectAccessor; _context = SynchronizationContext.Current;
        AddCommand = new RelayCommand(Add, () => Targets.Count > 0); RemoveCommand = new RelayCommand(Remove, () => Selected is not null); DuplicateCommand = new RelayCommand(Duplicate, () => Selected is not null);
        AddSourceCommand = new RelayCommand(AddSource, () => Selected is not null && Selected.Sources.Length < CabinetReflectionContract.MaximumSources && FaceChoices.Count > 0);
        RemoveSourceCommand = new RelayCommand(RemoveSource, () => SelectedSource is not null); DeriveCommand = new RelayCommand(Derive, () => SelectedSource is not null);
        RefreshCommand = new RelayCommand(Refresh); BrowseMaskCommand = new RelayCommand(BrowseMask, () => Selected is not null); ClearMaskCommand = new RelayCommand(() => Mask = string.Empty, () => Selected is not null);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<CabinetReflectionDefinition> Items { get; } = new();
    public ObservableCollection<CabinetReflectionReceiverTarget> Targets { get; } = new();
    public ObservableCollection<CabinetReflectionFaceChoice> FaceChoices { get; } = new();
    public ObservableCollection<CabinetReflectionSourceViewModel> Sources { get; } = new();
    public ObservableCollection<CabinetFaceTarget> FaceTargets { get; } = new();
    public ICommand AddCommand { get; } public ICommand RemoveCommand { get; } public ICommand DuplicateCommand { get; } public ICommand AddSourceCommand { get; } public ICommand RemoveSourceCommand { get; } public ICommand DeriveCommand { get; } public ICommand RefreshCommand { get; }
    public ICommand BrowseMaskCommand { get; } public ICommand ClearMaskCommand { get; }
    public IReadOnlyList<string> Presets { get; } = [CabinetReflectionPreset.RoughPlastic, CabinetReflectionPreset.PolishedChrome, CabinetReflectionPreset.Custom];
    public IReadOnlyList<string> PlaneSources { get; } = [CabinetReflectionPlaneSource.Automatic, CabinetReflectionPlaneSource.Manual];
    public CabinetReflectionDefinition? Selected { get => _selected; set { _selected = value; RebuildSources(); RaiseAll(); } }
    public CabinetReflectionSourceViewModel? SelectedSource { get => _selectedSource; set { _selectedSource = value; RaiseAll(); } }
    public bool HasSelection => Selected is not null;
    public string Id { get => Selected?.Id ?? string.Empty; set => Update(item => item with { Id = value }); }
    public bool Enabled { get => Selected?.Settings.Enabled ?? false; set => UpdateSettings(settings => settings with { Enabled = value }); }
    public CabinetReflectionReceiverTarget? Target { get => Targets.FirstOrDefault(item => item.TargetPath == Selected?.TargetId); set { if (value is not null) Update(item => item with { TargetId = value.TargetPath, MaterialSlot = 0 }); } }
    public IReadOnlyList<CabinetReflectionMaterialSlot> Slots => Target?.MaterialSlots ?? [];
    public CabinetReflectionMaterialSlot? Slot { get => Slots.FirstOrDefault(item => item.Index == Selected?.MaterialSlot); set { if (value is not null) Update(item => item with { MaterialSlot = value.Index }); } }
    public string Preset { get => Selected is null ? CabinetReflectionPreset.Custom : CabinetReflectionPreset.Detect(Selected.Settings); set => UpdateSettings(settings => CabinetReflectionPreset.Resolve(value, settings)); }
    public string Mask { get => Selected?.VisibilityMask ?? string.Empty; set => Update(item => item with { VisibilityMask = string.IsNullOrWhiteSpace(value) ? null : value.Trim() }); }
    public double Strength { get => Selected?.Settings.Strength ?? 0; set => UpdateSettings(s => s with { Strength = value }); } public double Artwork { get => Selected?.Settings.UnlitArtworkStrength ?? 0; set => UpdateSettings(s => s with { UnlitArtworkStrength = value }); } public double Lamps { get => Selected?.Settings.LitLampStrength ?? 0; set => UpdateSettings(s => s with { LitLampStrength = value }); } public double Roughness { get => Selected?.Settings.Roughness ?? 0; set => UpdateSettings(s => s with { Roughness = value }); } public double Distortion { get => Selected?.Settings.Distortion ?? 0; set => UpdateSettings(s => s with { Distortion = value }); }
    public double FresnelStrength { get => Selected?.Settings.FresnelStrength ?? 0; set => UpdateSettings(s => s with { FresnelStrength = value }); } public double FresnelPower { get => Selected?.Settings.FresnelPower ?? 0; set => UpdateSettings(s => s with { FresnelPower = value }); } public double EdgeFade { get => Selected?.Settings.EdgeFade ?? 0; set => UpdateSettings(s => s with { EdgeFade = value }); }
    public string Validation { get { if (Selected is null) return string.Empty; if (Items.Count(item => item.Id == Selected.Id) > 1) return "Duplicate receiver ID."; if (Target is null) return "Cabinet renderer is missing."; if (Slot is null) return "Material slot is invalid."; if (Selected.Settings.Enabled && Sources.Count == 0) return "Enabled receivers require at least one source Face."; if (Sources.Count > CabinetReflectionContract.MaximumSources) return $"A receiver supports at most {CabinetReflectionContract.MaximumSources} source Faces."; if (Sources.GroupBy(source => source.FaceId).Any(group => group.Count() > 1)) return "Duplicate source Face IDs are not allowed within a receiver."; foreach (var source in Sources) { if (source.Choice?.IsMissing != false) return $"Source Face '{source.FaceId}' is missing. Choose another Face or remove this source."; if (!CabinetReflectionPlaneValidation.TryValidate(source.Model.Plane, out var error)) return $"Source Face '{source.Choice.Label}' plane is invalid: {error}"; } return "Valid"; } }

    public void Initialize()
    {
        if (_attached || _disposed) return;
        _attached = true;
        Refresh();
        StartWatcher();
    }

    public void RefreshProjectContext()
    {
        if (_disposed) return;
        Refresh();
        if (_watcher is null) StartWatcher();
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_refreshGate)
        {
            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
            _refreshDebounce?.Dispose();
            _refreshDebounce = null;
        }
    }
    public void SetDiscovery(IEnumerable<CabinetReflectionReceiverTarget> targets, IEnumerable<CabinetFaceTarget> faceTargets) { Targets.Clear(); foreach (var item in targets) Targets.Add(item); FaceTargets.Clear(); foreach (var item in faceTargets) FaceTargets.Add(item); RaiseAll(); }
    public void Refresh()
    {
        if (_disposed) return;
        var receiverId = Selected?.Id; var sourceIndex = SelectedSource?.Index;
        List<CabinetReflectionFaceChoice> discovered;
        try { discovered = CabinetReflectionFaceCatalog.Discover(_projectAccessor?.Invoke()?.AssetsDirectory).ToList(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ObjectDisposedException or System.Security.SecurityException) { discovered = []; }
        var storedIds = (_document.GetCabinetDocument().Reflections ?? []).SelectMany(item => item.Sources ?? []).Select(item => item.FaceId).Where(id => !string.IsNullOrWhiteSpace(id));
        foreach (var missing in storedIds.Where(id => discovered.All(choice => choice.FaceId != id)).Distinct(StringComparer.Ordinal)) discovered.Add(new(missing, $"Missing Face ({missing})", string.Empty, $"Missing Face ({missing})", null, true));
        FaceChoices.Clear(); foreach (var choice in discovered) FaceChoices.Add(choice);
        Items.Clear(); foreach (var item in _document.GetCabinetDocument().Reflections ?? []) Items.Add(item);
        _selected = Items.FirstOrDefault(item => item.Id == receiverId) ?? Items.FirstOrDefault(); RebuildSources();
        SelectedSource = sourceIndex is int index ? Sources.ElementAtOrDefault(index) : Sources.FirstOrDefault(); RaiseAll();
    }
    private void StartWatcher()
    {
        if (_disposed || _watcher is not null) return;
        string? faces;
        try
        {
            var assets = _projectAccessor?.Invoke()?.AssetsDirectory;
            if (string.IsNullOrWhiteSpace(assets)) return;
            faces = Path.Combine(Path.GetFullPath(assets), "Faces");
            if (!Directory.Exists(faces)) return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return; }
        try
        {
            _watcher = new FileSystemWatcher(faces) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite };
            FileSystemEventHandler changed = (_, _) => ScheduleRefresh(); RenamedEventHandler renamed = (_, _) => ScheduleRefresh();
            _watcher.Created += changed; _watcher.Deleted += changed; _watcher.Changed += changed; _watcher.Renamed += renamed; _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ObjectDisposedException or System.Security.SecurityException)
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void ScheduleRefresh()
    {
        lock (_refreshGate)
        {
            if (_disposed) return;
            _refreshDebounce ??= new Timer(_ => _context?.Post(_ => { if (!_disposed) Refresh(); }, null), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _refreshDebounce.Change(TimeSpan.FromMilliseconds(150), Timeout.InfiniteTimeSpan);
        }
    }
    private void RebuildSources() { Sources.Clear(); if (_selected is not null) for (var i = 0; i < _selected.Sources.Length; i++) Sources.Add(new(this, i, _selected.Sources[i])); _selectedSource = Sources.FirstOrDefault(); }
    private void Add() { var target = Targets.FirstOrDefault(); if (target is null) return; var sources = FaceChoices.FirstOrDefault(choice => !choice.IsMissing) is { } choice ? new[] { NewSource(choice.FaceId) } : []; var item = new CabinetReflectionDefinition("reflection-" + Guid.NewGuid().ToString("N"), target.TargetPath, 0, sources, CabinetReflectionSettings.RoughPlastic); ExecuteAdd(item); if (Sources.Count > 0) Derive(); }
    private void ExecuteAdd(CabinetReflectionDefinition item) { _document.CommandService.Execute(CabinetMutationCommands.CreateAddReflectionCommand(_document.DocumentId, _document, item)); Refresh(); Selected = Items.First(candidate => candidate.Id == item.Id); }
    private static CabinetReflectionSource NewSource(string faceId) => new(faceId, new(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), 1, 1));
    private void Remove() { if (Selected is null) return; _document.CommandService.Execute(CabinetMutationCommands.CreateDeleteReflectionCommand(_document.DocumentId, _document, Selected.Id)); Refresh(); }
    private void Duplicate() { if (Selected is null) return; ExecuteAdd(Selected with { Id = "reflection-" + Guid.NewGuid().ToString("N"), Sources = Selected.Sources.Select(source => source with { Plane = source.Plane with { Origin = source.Plane.Origin with { }, Right = source.Plane.Right with { }, Up = source.Plane.Up with { } } }).ToArray() }); }
    private void AddSource() { if (Selected is null) return; var choice = FaceChoices.FirstOrDefault(candidate => !candidate.IsMissing && Selected.Sources.All(source => source.FaceId != candidate.FaceId)); if (choice is null) return; Update(item => item with { Sources = item.Sources.Append(NewSource(choice.FaceId)).ToArray() }); SelectedSource = Sources.LastOrDefault(); Derive(); }
    private void RemoveSource() { if (SelectedSource is null) return; var index = SelectedSource.Index; Update(item => item with { Sources = item.Sources.Where((_, i) => i != index).ToArray() }); }
    private void Derive() { if (SelectedSource is null) return; var targetId = SelectedSource.Choice?.CabinetTargetId; var target = FaceTargets.FirstOrDefault(item => item.Id == targetId); if (CabinetReflectionPlaneDeriver.TryDerive(target, out var plane, out _)) UpdateSource(SelectedSource.Index, source => source with { Plane = plane, PlaneSource = CabinetReflectionPlaneSource.Automatic }); }
    internal void UpdateSource(int index, Func<CabinetReflectionSource, CabinetReflectionSource> change) => Update(item => item with { Sources = item.Sources.Select((source, i) => i == index ? change(source) : source).ToArray() }, index);
    private void BrowseMask() { var dialog = new OpenFileDialog { Filter = "Image files|*.png;*.jpg;*.jpeg|All files|*.*" }; if (dialog.ShowDialog() == true) Mask = Path.GetRelativePath(Path.GetDirectoryName(_document.Document.FilePath) ?? string.Empty, dialog.FileName).Replace('\\', '/'); }
    private void UpdateSettings(Func<CabinetReflectionSettings, CabinetReflectionSettings> change) => Update(item => item with { Settings = change(item.Settings).Normalized() });
    private void Update(Func<CabinetReflectionDefinition, CabinetReflectionDefinition> change, int? sourceIndex = null) { if (Selected is null) return; var oldId = Selected.Id; var changed = change(Selected); _document.CommandService.Execute(CabinetMutationCommands.CreateUpdateReflectionCommand(_document.DocumentId, _document, oldId, changed)); _selected = changed; Refresh(); if (sourceIndex is int index) SelectedSource = Sources.ElementAtOrDefault(index); }
    private void RaiseAll([CallerMemberName] string? unused = null) { foreach (var name in new[] { nameof(Selected), nameof(SelectedSource), nameof(HasSelection), nameof(Id), nameof(Enabled), nameof(Target), nameof(Slots), nameof(Slot), nameof(Preset), nameof(Mask), nameof(Strength), nameof(Artwork), nameof(Lamps), nameof(Roughness), nameof(Distortion), nameof(FresnelStrength), nameof(FresnelPower), nameof(EdgeFade), nameof(Validation) }) PropertyChanged?.Invoke(this, new(name)); foreach (var command in new[] { AddCommand, RemoveCommand, DuplicateCommand, AddSourceCommand, RemoveSourceCommand, DeriveCommand }.OfType<RelayCommand>()) command.RaiseCanExecuteChanged(); }
}

public sealed class CabinetReflectionSourceViewModel : INotifyPropertyChanged
{
    private readonly CabinetReflectionEditorViewModel _owner; public int Index { get; } public CabinetReflectionSource Model { get; }
    public CabinetReflectionSourceViewModel(CabinetReflectionEditorViewModel owner, int index, CabinetReflectionSource model) { _owner = owner; Index = index; Model = model; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string FaceId => Model.FaceId;
    public CabinetReflectionFaceChoice? Choice { get => _owner.FaceChoices.FirstOrDefault(item => item.FaceId == Model.FaceId); set { if (value is not null) _owner.UpdateSource(Index, source => source with { FaceId = value.FaceId }); } }
    public string PlaneSource { get => Model.PlaneSource; set => _owner.UpdateSource(Index, source => source with { PlaneSource = value }); }
    public double OriginX { get => Model.Plane.Origin.X; set => Plane(plane => plane with { Origin = plane.Origin with { X = value } }); } public double OriginY { get => Model.Plane.Origin.Y; set => Plane(plane => plane with { Origin = plane.Origin with { Y = value } }); } public double OriginZ { get => Model.Plane.Origin.Z; set => Plane(plane => plane with { Origin = plane.Origin with { Z = value } }); }
    public double RightX { get => Model.Plane.Right.X; set => Plane(plane => plane with { Right = plane.Right with { X = value } }); } public double RightY { get => Model.Plane.Right.Y; set => Plane(plane => plane with { Right = plane.Right with { Y = value } }); } public double RightZ { get => Model.Plane.Right.Z; set => Plane(plane => plane with { Right = plane.Right with { Z = value } }); }
    public double UpX { get => Model.Plane.Up.X; set => Plane(plane => plane with { Up = plane.Up with { X = value } }); } public double UpY { get => Model.Plane.Up.Y; set => Plane(plane => plane with { Up = plane.Up with { Y = value } }); } public double UpZ { get => Model.Plane.Up.Z; set => Plane(plane => plane with { Up = plane.Up with { Z = value } }); } public double Width { get => Model.Plane.Width; set => Plane(plane => plane with { Width = value }); } public double Height { get => Model.Plane.Height; set => Plane(plane => plane with { Height = value }); }
    private void Plane(Func<CabinetReflectionPlane, CabinetReflectionPlane> change) => _owner.UpdateSource(Index, source => source with { Plane = change(source.Plane), PlaneSource = CabinetReflectionPlaneSource.Manual });
}
