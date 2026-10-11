using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace OasisEditor;

public sealed record PrefabNodeChoice(string Id, string Name);

public sealed partial class DocumentTabViewModel
{
    private string _prefabValidationMessage = string.Empty;
    private string? _prefabCleanContent;
    private PrefabPreviewViewModel? _prefabPreview;
    private readonly Dictionary<string, string> _pendingPrefabModelSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PrefabNodeChoice> _prefabChoices = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> PendingPrefabModelSources => _pendingPrefabModelSources;
    public ObservableCollection<PrefabNodeChoice> PrefabNodes { get; } = [];
    public string PrefabValidationMessage { get => _prefabValidationMessage; private set { _prefabValidationMessage = value; PropertyChanged?.Invoke(this, new(nameof(PrefabValidationMessage))); } }
    public PrefabNode SelectedPrefabNode => PrefabAuthoringService.Find(_object3DDocumentModel.Root, SelectionState.PrimaryItem is { Domain: EditorSelectionDomain.PrefabNode } item ? item.ObjectId : _object3DDocumentModel.Root.Id) ?? _object3DDocumentModel.Root;
    public PrefabNodeChoice? SelectedPrefabChoice
    {
        get => PrefabNodes.FirstOrDefault(n => n.Id == SelectedPrefabNode.Id);
        set { if (value is not null) SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.PrefabNode, value.Id)); }
    }
    public string PrefabNodeName { get => SelectedPrefabNode.Name; set => EditSelectedNode(n => n with { Name = value?.Trim() ?? string.Empty }, "Rename Prefab node"); }
    public string PrefabNodeId => SelectedPrefabNode.Id;
    public IReadOnlyList<PrefabComponent> PrefabComponents => SelectedPrefabNode.Components;
    public IReadOnlyList<PrefabNodeChoice> PrefabParentChoices => PrefabNodes.Where(n => PrefabAuthoringService.Find(SelectedPrefabNode, n.Id) is null).ToArray();
    public PrefabNodeChoice? PrefabParent
    {
        get => PrefabNodes.FirstOrDefault(n => n.Id == PrefabAuthoringService.Parent(_object3DDocumentModel.Root, SelectedPrefabNode.Id)?.Id);
        set
        {
            if (value is null || PrefabParent?.Id == value.Id) return;
            MutatePrefab(() => PrefabAuthoringService.Move(_object3DDocumentModel, SelectedPrefabNode.Id, value.Id, PrefabAuthoringService.Find(_object3DDocumentModel.Root, value.Id)!.Children.Count), "Reparent Prefab node");
        }
    }
    public bool CanRemovePrefabNode => SelectedPrefabNode.Id != _object3DDocumentModel.Root.Id;
    public bool HasPrefabModel { get => SelectedPrefabNode.Component(PrefabComponentKind.Model) is not null; set => SetPrefabComponent(PrefabComponentKind.Model, value); }
    public bool HasPrefabCollider { get => SelectedPrefabNode.Component(PrefabComponentKind.Collider) is not null; set => SetPrefabComponent(PrefabComponentKind.Collider, value); }
    public bool HasPrefabRigidbody { get => SelectedPrefabNode.Component(PrefabComponentKind.Rigidbody) is not null; set => SetPrefabComponent(PrefabComponentKind.Rigidbody, value); }
    public IReadOnlyList<PrefabColliderSourceFrame> PrefabColliderFrames { get; } = Enum.GetValues<PrefabColliderSourceFrame>();
    public PrefabColliderSourceFrame PrefabColliderFrame
    {
        get => SelectedPrefabNode.Component(PrefabComponentKind.Collider)?.SourceFrame ?? PrefabColliderSourceFrame.NodeLocalSI;
        set => EditSelectedComponent(PrefabComponentKind.Collider, c => c with { SourceFrame = value }, "Set collider source frame");
    }
    public bool PrefabColliderIsTrigger { get => SelectedPrefabNode.Component(PrefabComponentKind.Collider)?.IsTrigger ?? false; set => EditSelectedComponent(PrefabComponentKind.Collider, c => c with { IsTrigger = value }, "Set collider trigger role"); }
    public IReadOnlyList<PrefabBodyMode> PrefabBodyModes { get; } = Enum.GetValues<PrefabBodyMode>();
    public PrefabBodyMode PrefabBodyMode
    {
        get => SelectedPrefabNode.Component(PrefabComponentKind.Rigidbody)?.Mode ?? global::OasisEditor.PrefabBodyMode.Static;
        set => EditSelectedComponent(PrefabComponentKind.Rigidbody, c => c with { Mode = value, Rigidbody = value == global::OasisEditor.PrefabBodyMode.Static ? new(false) : new(true, c.Rigidbody?.Mass ?? 1, c.Rigidbody?.UseGravity ?? true) }, "Set body mode");
    }
    private MachineObjectTransform SelectedPrefabTransform => SelectedPrefabNode.Component(PrefabComponentKind.Transform)!.Transform!;
    public double PrefabPositionX { get => SelectedPrefabTransform.Position.X; set => SetPrefabTransform(0, 0, value); }
    public double PrefabPositionY { get => SelectedPrefabTransform.Position.Y; set => SetPrefabTransform(0, 1, value); }
    public double PrefabPositionZ { get => SelectedPrefabTransform.Position.Z; set => SetPrefabTransform(0, 2, value); }
    public double PrefabRotationX { get => SelectedPrefabTransform.Rotation.X; set => SetPrefabTransform(1, 0, value); }
    public double PrefabRotationY { get => SelectedPrefabTransform.Rotation.Y; set => SetPrefabTransform(1, 1, value); }
    public double PrefabRotationZ { get => SelectedPrefabTransform.Rotation.Z; set => SetPrefabTransform(1, 2, value); }
    public double PrefabScaleX { get => SelectedPrefabTransform.Scale.X; set => SetPrefabTransform(2, 0, value); }
    public double PrefabScaleY { get => SelectedPrefabTransform.Scale.Y; set => SetPrefabTransform(2, 1, value); }
    public double PrefabScaleZ { get => SelectedPrefabTransform.Scale.Z; set => SetPrefabTransform(2, 2, value); }
    public ICommand AddPrefabNodeCommand => _addPrefabNodeCommand ??= new RelayCommand(AddPrefabNode);
    public ICommand RemovePrefabNodeCommand => _removePrefabNodeCommand ??= new RelayCommand(() => MutatePrefab(() => PrefabAuthoringService.Remove(_object3DDocumentModel, SelectedPrefabNode.Id), "Remove Prefab subtree"), () => CanRemovePrefabNode);
    public ICommand MovePrefabNodeUpCommand => _movePrefabNodeUpCommand ??= new RelayCommand(() => ReorderPrefabNode(-1));
    public ICommand MovePrefabNodeDownCommand => _movePrefabNodeDownCommand ??= new RelayCommand(() => ReorderPrefabNode(1));
    private RelayCommand? _addPrefabNodeCommand, _removePrefabNodeCommand, _movePrefabNodeUpCommand, _movePrefabNodeDownCommand;
    public PrefabPreviewViewModel? PrefabPreview => Document.DocumentType == EditorDocumentType.Object3D ? _prefabPreview ??= new PrefabPreviewViewModel(this, new Features.CabinetEditor.Services.SharpGltfWpfModelLoader(interpretCabinetSemantics: false)) : null;

    public void AddPrefabNode()
    {
        var node = PrefabNode.Create("Node");
        if (MutatePrefab(() => PrefabAuthoringService.Add(_object3DDocumentModel, SelectedPrefabNode.Id, node), "Add Prefab node"))
            SelectionState.Replace(new EditorSelectionItem(EditorSelectionDomain.PrefabNode, node.Id));
    }
    public void ReorderPrefabNode(int delta)
    {
        var parent = PrefabAuthoringService.Parent(_object3DDocumentModel.Root, SelectedPrefabNode.Id);
        if (parent is null) return;
        var index = parent.Children.ToList().FindIndex(n => n.Id == SelectedPrefabNode.Id);
        var target = index + delta;
        if (target < 0 || target >= parent.Children.Count) return;
        MutatePrefab(() => PrefabAuthoringService.Move(_object3DDocumentModel, SelectedPrefabNode.Id, parent.Id, target), "Reorder Prefab node");
    }
    public bool MutatePrefab(Func<Object3DDocument> mutation, string description)
    {
        try { var next = mutation(); SetObject3DValue(next, description); return string.IsNullOrEmpty(PrefabValidationMessage); }
        catch (InvalidOperationException exception) { PrefabValidationMessage = exception.Message; NotifyPrefabProperties(); return false; }
    }
    private void EditSelectedNode(Func<PrefabNode, PrefabNode> mutation, string description) => MutatePrefab(() => PrefabAuthoringService.Edit(_object3DDocumentModel, SelectedPrefabNode.Id, mutation), description);
    private void SetPrefabComponent(PrefabComponentKind kind, bool value) => MutatePrefab(() => PrefabAuthoringService.Component(_object3DDocumentModel, SelectedPrefabNode.Id, kind, value), value ? "Add Prefab component" : "Remove Prefab component");
    private void EditSelectedComponent(PrefabComponentKind kind, Func<PrefabComponent, PrefabComponent> mutation, string description) => EditSelectedNode(n => n.Component(kind) is { } c ? n.SetComponent(mutation(c)) : n, description);
    private void SetPrefabTransform(int field, int axis, double value)
    {
        EditSelectedComponent(PrefabComponentKind.Transform, c =>
        {
            var t = c.Transform!;
            var v = field == 0 ? t.Position : field == 1 ? t.Rotation : t.Scale;
            v = axis == 0 ? v with { X = value } : axis == 1 ? v with { Y = value } : v with { Z = value };
            return c with { Transform = field == 0 ? t with { Position = v } : field == 1 ? t with { Rotation = v } : t with { Scale = v } };
        }, "Edit Prefab local transform");
    }
    internal void ApplyPrefabEdit(Object3DDocument value)
    {
        _object3DDocumentModel = value;
        ReconcileSelection();
        var clean = _prefabCleanContent == Object3DDocumentStorage.Serialize(value) && !_pendingPrefabModelSources.Keys.Any(id => PrefabAuthoringService.Find(value.Root, id) is not null);
        if (clean) { _document = _document.MarkClean(); NotifyDocumentMetadataChanged(); } else MarkDirty();
        RefreshPrefabChoices();
        PropertyChanged?.Invoke(this, new("PrefabDocument"));
        NotifyPrefabProperties();
        _prefabPreview?.Refresh();
    }
    private void RefreshPrefabChoices()
    {
        var nodes = Object3DValidationService.Descendants(_object3DDocumentModel.Root).ToArray();
        var choices = new List<PrefabNodeChoice>();
        foreach (var node in nodes)
        {
            if (!_prefabChoices.TryGetValue(node.Id, out var row) || row.Name != node.Name) row = new(node.Id, node.Name);
            _prefabChoices[node.Id] = row; choices.Add(row);
        }
        for (var i = PrefabNodes.Count - 1; i >= 0; i--) if (!choices.Contains(PrefabNodes[i])) PrefabNodes.RemoveAt(i);
        for (var i = 0; i < choices.Count; i++)
        {
            var current = PrefabNodes.IndexOf(choices[i]);
            if (current < 0) PrefabNodes.Insert(i, choices[i]); else if (current != i) PrefabNodes.Move(current, i);
        }
        foreach (var id in _prefabChoices.Keys.Where(id => nodes.All(n => n.Id != id)).ToArray()) _prefabChoices.Remove(id);
    }
    private void NotifyPrefabProperties()
    {
        foreach (var name in SetObject3DDocumentCommand.Object3DPropertyNames) PropertyChanged?.Invoke(this, new(name));
        foreach (var name in new[] { nameof(SelectedPrefabChoice), nameof(PrefabNodeName), nameof(PrefabNodeId), nameof(PrefabComponents), nameof(PrefabParentChoices), nameof(PrefabParent), nameof(CanRemovePrefabNode), nameof(HasPrefabModel), nameof(HasPrefabCollider), nameof(HasPrefabRigidbody), nameof(PrefabColliderFrame), nameof(PrefabColliderIsTrigger), nameof(PrefabBodyMode), nameof(PrefabPositionX), nameof(PrefabPositionY), nameof(PrefabPositionZ), nameof(PrefabRotationX), nameof(PrefabRotationY), nameof(PrefabRotationZ), nameof(PrefabScaleX), nameof(PrefabScaleY), nameof(PrefabScaleZ) }) PropertyChanged?.Invoke(this, new(name));
        _removePrefabNodeCommand?.RaiseCanExecuteChanged();
    }
}
