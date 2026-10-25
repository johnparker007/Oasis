using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OasisEditor.Features.PrefabEditor.ViewModels;

namespace OasisEditor;

/// <summary>Command-backed hierarchy and Inspector projection for an authored Prefab.</summary>
public sealed class PrefabAuthoringViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentTabViewModel _document;
    private string? _selectedNodeId;
    private string _selectedParentNodeId = string.Empty;
    private string _diagnostic = string.Empty;
    private bool _disposed;

    public PrefabAuthoringViewModel(DocumentTabViewModel document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Preview = new PrefabPreviewViewModel(document);
        AddChildCommand = new RelayCommand(AddChild);
        RemoveNodeCommand = new RelayCommand(RemoveSelected, () => SelectedNode is not null && SelectedNode.Id != Document.Root.Id);
        MoveUpCommand = new RelayCommand(() => MoveSelected(-1), CanMoveUp);
        MoveDownCommand = new RelayCommand(() => MoveSelected(1), CanMoveDown);
        AddModelCommand = new RelayCommand(AddModel);
        AddColliderCommand = new RelayCommand(AddCollider);
        AddRigidbodyCommand = new RelayCommand(AddRigidbody);
        RemoveComponentCommand = new RelayCommand(RemoveSelectedComponent, () => SelectedComponent is not null && SelectedComponent.Kind != Object3DComponentDefinition.TransformKind);
        ReparentSelectedCommand = new RelayCommand(() => { if (SelectedNode is { } node && !string.IsNullOrWhiteSpace(SelectedParentNodeId)) ReparentNode(node.Id, SelectedParentNodeId); }, CanReparentSelected);
        _selectedNodeId = Document.Root.Id;
        Refresh();
        _document.PropertyChanged += OnDocumentPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<PrefabHierarchyRow> Nodes { get; } = [];
    public ObservableCollection<PrefabHierarchyRow> ReparentTargets { get; } = [];
    public ObservableCollection<Object3DComponentDefinition> Components { get; } = [];
    public PrefabPreviewViewModel Preview { get; }
    public ICommand AddChildCommand { get; }
    public ICommand RemoveNodeCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand AddModelCommand { get; }
    public ICommand AddColliderCommand { get; }
    public ICommand AddRigidbodyCommand { get; }
    public ICommand RemoveComponentCommand { get; }
    public ICommand ReparentSelectedCommand { get; }

    public Object3DDocument Document => _document.GetObject3DDocument();
    public string DisplayName
    {
        get => Document.DisplayName;
        set => Apply(Document with { DisplayName = value?.Trim() ?? string.Empty }, "Rename Prefab");
    }
    public string SelectedNodeId
    {
        get => _selectedNodeId ?? Document.Root.Id;
        set
        {
            if (string.IsNullOrEmpty(value) || FindNode(Document.Root, value) is null || value == _selectedNodeId) return;
            _selectedNodeId = value;
            RefreshSelection();
        }
    }
    public string SelectedParentNodeId
    {
        get => _selectedParentNodeId;
        set { if (_selectedParentNodeId == value) return; _selectedParentNodeId = value; Notify(); if (ReparentSelectedCommand is RelayCommand relay) relay.RaiseCanExecuteChanged(); }
    }
    public PrefabHierarchyRow? SelectedRow => Nodes.FirstOrDefault(row => row.Id == SelectedNodeId);
    public Object3DNodeDefinition? SelectedNode => FindNode(Document.Root, SelectedNodeId);
    public string SelectedNodeName
    {
        get => SelectedNode?.Name ?? string.Empty;
        set
        {
            var node = SelectedNode;
            if (node is null) return;
            UpdateNode(node.Id, current => current with { Name = value?.Trim() ?? string.Empty }, "Rename Prefab node");
        }
    }
    public Object3DComponentDefinition? SelectedComponent { get; private set; }
    public string SelectedComponentId
    {
        get => SelectedComponent?.ComponentId ?? string.Empty;
        set
        {
            SelectedComponent = Components.FirstOrDefault(component => component.ComponentId == value);
            Notify();
            NotifyInspector();
            RaiseCommandStates();
        }
    }
    public string Diagnostic { get => _diagnostic; private set { if (_diagnostic == value) return; _diagnostic = value; Notify(); Notify(nameof(HasDiagnostic)); } }
    public bool HasDiagnostic => !string.IsNullOrWhiteSpace(Diagnostic);
    public string ModelPath { get => SelectedComponent?.Model?.Path ?? string.Empty; set => EditSelectedComponent(Object3DComponentDefinition.ModelKind, c => c with { Model = c.Model! with { Path = value?.Trim() ?? string.Empty } }, "Set Model path"); }
    public double ModelScale { get => SelectedComponent?.Model?.Scale ?? 1; set => EditSelectedComponent(Object3DComponentDefinition.ModelKind, c => c with { Model = c.Model! with { Scale = value } }, "Set Model scale"); }
    public string ModelUpAxis { get => SelectedComponent?.Model?.UpAxis ?? "Y"; set => EditSelectedComponent(Object3DComponentDefinition.ModelKind, c => c with { Model = c.Model! with { UpAxis = value } }, "Set Model up-axis"); }
    public string[] UpAxes { get; } = ["X", "Y", "Z"];
    public Object3DColliderKind ColliderKind { get => SelectedComponent?.Collider?.Kind ?? Object3DColliderKind.None; set => EditCollider(c => CreateCollider(value, c), "Set Collider kind"); }
    public Object3DColliderKind[] ColliderKinds { get; } = Enum.GetValues<Object3DColliderKind>();
    public bool ColliderIsTrigger { get => SelectedComponent?.Collider?.IsTrigger ?? false; set => EditCollider(c => c with { IsTrigger = value }, "Set Collider trigger"); }
    public Object3DColliderSourceFrame ColliderSourceFrame { get => SelectedComponent?.Collider?.SourceFrame ?? Object3DColliderSourceFrame.ModelLocal; set => EditCollider(c => c with { SourceFrame = value }, "Set Collider source frame"); }
    public Object3DColliderSourceFrame[] ColliderSourceFrames { get; } = Enum.GetValues<Object3DColliderSourceFrame>();
    public double ColliderCenterX { get => ColliderComponent?.Collider?.Center?.ElementAtOrDefault(0) ?? 0; set => EditCollider(c => c with { Center = [value, c.Center?.ElementAtOrDefault(1) ?? 0, c.Center?.ElementAtOrDefault(2) ?? 0] }, "Set Collider center"); }
    public double ColliderCenterY { get => ColliderComponent?.Collider?.Center?.ElementAtOrDefault(1) ?? 0; set => EditCollider(c => c with { Center = [c.Center?.ElementAtOrDefault(0) ?? 0, value, c.Center?.ElementAtOrDefault(2) ?? 0] }, "Set Collider center"); }
    public double ColliderCenterZ { get => ColliderComponent?.Collider?.Center?.ElementAtOrDefault(2) ?? 0; set => EditCollider(c => c with { Center = [c.Center?.ElementAtOrDefault(0) ?? 0, c.Center?.ElementAtOrDefault(1) ?? 0, value] }, "Set Collider center"); }
    public double ColliderRadius { get => ColliderComponent?.Collider?.Radius ?? .5; set => EditCollider(c => c with { Radius = value }, "Set Collider radius"); }
    public double ColliderSizeX { get => ColliderComponent?.Collider?.Size?.ElementAtOrDefault(0) ?? 1; set => EditCollider(c => c with { Size = [value, c.Size?.ElementAtOrDefault(1) ?? 1, c.Size?.ElementAtOrDefault(2) ?? 1] }, "Set Collider size"); }
    public double ColliderSizeY { get => ColliderComponent?.Collider?.Size?.ElementAtOrDefault(1) ?? 1; set => EditCollider(c => c with { Size = [c.Size?.ElementAtOrDefault(0) ?? 1, value, c.Size?.ElementAtOrDefault(2) ?? 1] }, "Set Collider size"); }
    public double ColliderSizeZ { get => ColliderComponent?.Collider?.Size?.ElementAtOrDefault(2) ?? 1; set => EditCollider(c => c with { Size = [c.Size?.ElementAtOrDefault(0) ?? 1, c.Size?.ElementAtOrDefault(1) ?? 1, value] }, "Set Collider size"); }
    public double ColliderHeight { get => ColliderComponent?.Collider?.Height ?? 2; set => EditCollider(c => c with { Height = value }, "Set Collider height"); }
    public Object3DCapsuleAxis ColliderAxis { get => ColliderComponent?.Collider?.Axis ?? Object3DCapsuleAxis.Y; set => EditCollider(c => c with { Axis = value }, "Set Collider axis"); }
    public Object3DCapsuleAxis[] CapsuleAxes { get; } = Enum.GetValues<Object3DCapsuleAxis>();
    public Object3DRigidbodyMode RigidbodyMode { get => SelectedComponent?.Rigidbody?.Mode ?? Object3DRigidbodyMode.Static; set => EditSelectedComponent(Object3DComponentDefinition.RigidbodyKind, c => c with { Rigidbody = c.Rigidbody! with { Mode = value, Mass = value == Object3DRigidbodyMode.Dynamic ? c.Rigidbody!.Mass ?? 1 : null } }, "Set Rigidbody mode"); }
    public Object3DRigidbodyMode[] RigidbodyModes { get; } = Enum.GetValues<Object3DRigidbodyMode>();
    public double RigidbodyMass { get => SelectedComponent?.Rigidbody?.Mass ?? 1; set => EditSelectedComponent(Object3DComponentDefinition.RigidbodyKind, c => c with { Rigidbody = c.Rigidbody! with { Mass = value } }, "Set Rigidbody mass"); }
    public bool RigidbodyUseGravity { get => SelectedComponent?.Rigidbody?.UseGravity ?? false; set => EditSelectedComponent(Object3DComponentDefinition.RigidbodyKind, c => c with { Rigidbody = c.Rigidbody! with { UseGravity = value } }, "Set Rigidbody gravity"); }

    public double PositionX { get => SelectedNode?.Transform.Position.X ?? 0; set => EditTransform(t => t with { Position = t.Position with { X = value } }); }
    public double PositionY { get => SelectedNode?.Transform.Position.Y ?? 0; set => EditTransform(t => t with { Position = t.Position with { Y = value } }); }
    public double PositionZ { get => SelectedNode?.Transform.Position.Z ?? 0; set => EditTransform(t => t with { Position = t.Position with { Z = value } }); }
    public double RotationX { get => SelectedNode?.Transform.Rotation.X ?? 0; set => EditTransform(t => t with { Rotation = t.Rotation with { X = value } }); }
    public double RotationY { get => SelectedNode?.Transform.Rotation.Y ?? 0; set => EditTransform(t => t with { Rotation = t.Rotation with { Y = value } }); }
    public double RotationZ { get => SelectedNode?.Transform.Rotation.Z ?? 0; set => EditTransform(t => t with { Rotation = t.Rotation with { Z = value } }); }
    public double ScaleX { get => SelectedNode?.Transform.Scale.X ?? 1; set => EditTransform(t => t with { Scale = t.Scale with { X = value } }); }
    public double ScaleY { get => SelectedNode?.Transform.Scale.Y ?? 1; set => EditTransform(t => t with { Scale = t.Scale with { Y = value } }); }
    public double ScaleZ { get => SelectedNode?.Transform.Scale.Z ?? 1; set => EditTransform(t => t with { Scale = t.Scale with { Z = value } }); }

    public void Refresh()
    {
        if (_disposed) return;
        if (FindNode(Document.Root, _selectedNodeId ?? string.Empty) is null) _selectedNodeId = Document.Root.Id;
        Nodes.Clear();
        Flatten(Document.Root, 0, Nodes);
        RefreshSelection();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.PropertyChanged -= OnDocumentPropertyChanged;
        Preview.Dispose();
    }

    private void AddChild()
    {
        var parent = SelectedNode;
        if (parent is null) return;
        var childId = AddChild(parent.Id, "New Node");
        if (childId is not null) SelectedNodeId = childId;
    }
    public string? AddChild(string parentNodeId, string name = "New Node")
    {
        var parent = FindNode(Document.Root, parentNodeId);
        if (parent is null) { Diagnostic = $"Prefab node '{parentNodeId}' no longer exists."; return null; }
        var child = new Object3DNodeDefinition(Guid.NewGuid().ToString("D"), string.IsNullOrWhiteSpace(name) ? "New Node" : name.Trim(), [Object3DComponentDefinition.CreateTransform()], []);
        UpdateNode(parent.Id, current => current with { Children = [.. current.Children, child] }, "Add Prefab child");
        return FindNode(Document.Root, child.Id) is null ? null : child.Id;
    }
    private void RemoveSelected()
    {
        var id = SelectedNodeId;
        var parentId = FindParent(Document.Root, id)?.Id;
        if (RemoveNode(id) && parentId is not null) SelectedNodeId = parentId;
    }
    public bool RemoveNode(string id)
    {
        if (Document.Root.Id == id) { Diagnostic = "The Prefab root cannot be removed."; return false; }
        var parent = FindParent(Document.Root, id);
        if (parent is null) { Diagnostic = $"Prefab node '{id}' no longer exists."; return false; }
        var siblings = parent.Children.Where(child => child.Id != id).ToArray();
        UpdateNode(parent.Id, current => current with { Children = siblings }, "Remove Prefab node");
        return FindNode(Document.Root, id) is null;
    }
    private void MoveSelected(int direction)
    {
        var node = SelectedNode;
        var parent = node is null ? null : FindParent(Document.Root, node.Id);
        if (node is null || parent is null) return;
        var index = Array.FindIndex(parent.Children, child => child.Id == node.Id);
        ReorderNode(node.Id, index + direction);
    }
    public bool ReorderNode(string nodeId, int targetIndex)
    {
        var parent = FindParent(Document.Root, nodeId);
        if (parent is null) { Diagnostic = $"Prefab node '{nodeId}' cannot be reordered at the root."; return false; }
        var sourceIndex = Array.FindIndex(parent.Children, child => child.Id == nodeId);
        if (targetIndex < 0 || targetIndex >= parent.Children.Length) { Diagnostic = "The target order is outside the sibling list."; return false; }
        if (sourceIndex == targetIndex) return false;
        var children = parent.Children.ToArray();
        var moving = children[sourceIndex];
        children = children.Where(child => child.Id != nodeId).ToArray();
        children = children.Take(targetIndex).Append(moving).Concat(children.Skip(targetIndex)).ToArray();
        UpdateNode(parent.Id, current => current with { Children = children }, targetIndex < sourceIndex ? "Move Prefab node up" : "Move Prefab node down");
        return true;
    }
    public bool ReparentNode(string nodeId, string newParentId, int? targetIndex = null)
    {
        var root = Document.Root;
        var moving = FindNode(root, nodeId);
        var oldParent = FindParent(root, nodeId);
        var newParent = FindNode(root, newParentId);
        if (moving is null || oldParent is null || newParent is null || nodeId == root.Id)
        { Diagnostic = "The selected node or destination parent is unavailable; the root cannot be reparented."; return false; }
        if (nodeId == newParentId || ContainsNode(moving, newParentId))
        { Diagnostic = $"Cannot reparent '{moving.Name}' beneath itself or one of its descendants."; return false; }
        var oldIndex = Array.FindIndex(oldParent.Children, child => child.Id == nodeId);
        if (oldParent.Id == newParentId && (targetIndex is null || targetIndex.Value == oldIndex)) return false;
        var without = ReplaceNode(root, oldParent.Id, parent => parent with { Children = parent.Children.Where(child => child.Id != nodeId).ToArray() });
        var targetParent = FindNode(without, newParentId);
        if (targetParent is null) { Diagnostic = "The destination parent is no longer available."; return false; }
        var children = targetParent.Children.ToList();
        var insertion = Math.Clamp(targetIndex ?? children.Count, 0, children.Count);
        children.Insert(insertion, moving);
        Apply(Document.WithRoot(ReplaceNode(without, newParentId, parent => parent with { Children = children.ToArray() })), "Reparent Prefab node");
        return FindParent(Document.Root, nodeId)?.Id == newParentId;
    }
    public bool RenameNode(string nodeId, string name)
    {
        if (FindNode(Document.Root, nodeId) is null) { Diagnostic = $"Prefab node '{nodeId}' no longer exists."; return false; }
        UpdateNode(nodeId, node => node with { Name = name?.Trim() ?? string.Empty }, "Rename Prefab node");
        return FindNode(Document.Root, nodeId)?.Name == (name?.Trim() ?? string.Empty);
    }
    public bool SetNodeTransform(string nodeId, MachineObjectTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var node = FindNode(Document.Root, nodeId);
        if (node is null) { Diagnostic = $"Prefab node '{nodeId}' no longer exists."; return false; }
        UpdateNode(nodeId, current => current.WithComponent(current.Components.Single(component => component.Kind == Object3DComponentDefinition.TransformKind) with { Transform = transform }), "Edit Prefab node transform");
        return FindNode(Document.Root, nodeId)?.Transform == transform;
    }
    public bool AddComponentToNode(string nodeId, Object3DComponentDefinition component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var node = FindNode(Document.Root, nodeId);
        if (node is null) { Diagnostic = $"Prefab node '{nodeId}' no longer exists."; return false; }
        if (node.Components.Any(existing => existing.Kind == component.Kind)) { Diagnostic = $"Node '{node.Name}' already has a {component.Kind} component."; return false; }
        UpdateNode(nodeId, current => current.WithComponent(component), $"Add {component.Kind} component");
        return FindNode(Document.Root, nodeId)?.Components.Any(existing => existing.ComponentId == component.ComponentId) == true;
    }
    public bool RemoveComponent(string nodeId, string componentId)
    {
        var node = FindNode(Document.Root, nodeId);
        var component = node?.Components.FirstOrDefault(entry => entry.ComponentId == componentId);
        if (node is null || component is null) { Diagnostic = $"Component '{componentId}' is unavailable on node '{nodeId}'."; return false; }
        if (component.Kind == Object3DComponentDefinition.TransformKind) { Diagnostic = "Every Prefab node must keep its Transform component."; return false; }
        UpdateNode(nodeId, current => current.WithoutComponent(component.Kind), $"Remove {component.Kind} component");
        return FindNode(Document.Root, nodeId)?.Components.All(entry => entry.ComponentId != componentId) == true;
    }
    public bool UpdateComponent(string nodeId, string componentId, Object3DComponentDefinition replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var node = FindNode(Document.Root, nodeId);
        var existing = node?.Components.FirstOrDefault(entry => entry.ComponentId == componentId);
        if (node is null || existing is null) { Diagnostic = $"Component '{componentId}' is unavailable on node '{nodeId}'."; return false; }
        if (replacement.ComponentId != componentId || replacement.Kind != existing.Kind)
        { Diagnostic = "Component edits must preserve the stable component ID and kind."; return false; }
        if (replacement == existing) return false;
        UpdateNode(nodeId, current => current.WithComponent(replacement), $"Edit {existing.Kind} component");
        return FindNode(Document.Root, nodeId)?.Components.Any(entry => entry.ComponentId == componentId && entry == replacement) == true;
    }
    private void AddModel() => AddComponent(Object3DComponentDefinition.CreateModel(new Object3DModelDefinition("model.glb", 1, "Y")), "Add Model component");
    private void AddCollider() => AddComponent(Object3DComponentDefinition.CreateCollider(new Object3DColliderDefinition(Object3DColliderKind.None, SourceFrame: Object3DColliderSourceFrame.NodeLocal)), "Add Collider component");
    private void AddRigidbody() => AddComponent(Object3DComponentDefinition.CreateRigidbody(new Object3DRigidbodyDefinition(Object3DRigidbodyMode.Static)), "Add Rigidbody component");
    private void AddComponent(Object3DComponentDefinition component, string description)
    {
        var node = SelectedNode;
        if (node is null || node.Components.Any(existing => existing.Kind == component.Kind)) return;
        UpdateNode(node.Id, current => current.WithComponent(component), description);
        SelectedComponentId = component.ComponentId;
    }
    private void RemoveSelectedComponent()
    {
        var component = SelectedComponent;
        var node = SelectedNode;
        if (component is null || node is null || component.Kind == Object3DComponentDefinition.TransformKind) return;
        UpdateNode(node.Id, current => current.WithoutComponent(component.Kind), $"Remove {component.Kind} component");
    }
    private void EditTransform(Func<MachineObjectTransform, MachineObjectTransform> edit)
    {
        var node = SelectedNode;
        if (node is null) return;
        var transform = node.Transform;
        UpdateNode(node.Id, current => current.WithComponent(current.Components.Single(c => c.Kind == Object3DComponentDefinition.TransformKind) with { Transform = edit(transform) }), "Edit Prefab node transform");
    }
    private void EditCollider(Func<Object3DColliderDefinition, Object3DColliderDefinition> edit, string description)
    {
        var current = SelectedComponent?.Collider;
        if (current is null) return;
        EditSelectedComponent(Object3DComponentDefinition.ColliderKind, c => c with { Collider = edit(current) }, description);
    }
    private Object3DComponentDefinition? ColliderComponent => SelectedComponent?.Kind == Object3DComponentDefinition.ColliderKind ? SelectedComponent : null;
    private void EditSelectedComponent(string kind, Func<Object3DComponentDefinition, Object3DComponentDefinition> edit, string description)
    {
        var node = SelectedNode;
        var component = SelectedComponent;
        if (node is null || component?.Kind != kind) return;
        UpdateNode(node.Id, current => current.WithComponent(edit(component)), description);
    }
    private void UpdateNode(string id, Func<Object3DNodeDefinition, Object3DNodeDefinition> edit, string description)
    {
        var root = ReplaceNode(Document.Root, id, edit);
        if (ReferenceEquals(root, Document.Root) || root == Document.Root) return;
        Apply(Document.WithRoot(root), description);
    }
    private void Apply(Object3DDocument next, string description)
    {
        try
        {
            Object3DValidationService.Validate(next);
            _document.SetObject3DDocumentFromAuthoring(next, description);
            Diagnostic = string.Empty;
        }
        catch (InvalidOperationException exception) { Diagnostic = exception.Message; }
        Refresh();
    }
    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DocumentTabViewModel.Object3DDisplayName) or nameof(DocumentTabViewModel.Object3DModelPath)) Refresh();
    }
    private void RefreshSelection()
    {
        var node = SelectedNode;
        ReparentTargets.Clear();
        foreach (var row in Nodes)
            if (node is null || !ContainsNode(node, row.Id)) ReparentTargets.Add(row);
        if (!ReparentTargets.Any(row => row.Id == SelectedParentNodeId)) SelectedParentNodeId = ReparentTargets.FirstOrDefault()?.Id ?? string.Empty;
        Components.Clear();
        if (node is not null) foreach (var component in node.Components) Components.Add(component);
        SelectedComponent = Components.FirstOrDefault(component => component.ComponentId == SelectedComponent?.ComponentId) ?? Components.FirstOrDefault();
        Notify(nameof(Document)); Notify(nameof(DisplayName)); Notify(nameof(SelectedNodeId)); Notify(nameof(SelectedRow)); Notify(nameof(SelectedNode)); Notify(nameof(SelectedNodeName)); Notify(nameof(SelectedComponent)); Notify(nameof(SelectedComponentId));
        NotifyInspector();
        RaiseCommandStates();
    }
    private void NotifyInspector()
    {
        foreach (var property in new[] { nameof(ModelPath), nameof(ModelScale), nameof(ModelUpAxis), nameof(ColliderKind), nameof(ColliderIsTrigger), nameof(ColliderSourceFrame), nameof(ColliderCenterX), nameof(ColliderCenterY), nameof(ColliderCenterZ), nameof(ColliderRadius), nameof(ColliderSizeX), nameof(ColliderSizeY), nameof(ColliderSizeZ), nameof(ColliderHeight), nameof(ColliderAxis), nameof(RigidbodyMode), nameof(RigidbodyMass), nameof(RigidbodyUseGravity), nameof(PositionX), nameof(PositionY), nameof(PositionZ), nameof(RotationX), nameof(RotationY), nameof(RotationZ), nameof(ScaleX), nameof(ScaleY), nameof(ScaleZ) }) Notify(property);
    }
    private void RaiseCommandStates()
    {
        foreach (var command in new[] { RemoveNodeCommand, MoveUpCommand, MoveDownCommand, RemoveComponentCommand }) if (command is RelayCommand relay) relay.RaiseCanExecuteChanged();
        if (ReparentSelectedCommand is RelayCommand reparent) reparent.RaiseCanExecuteChanged();
    }
    private bool CanMoveUp() => CanMove(-1);
    private bool CanMoveDown() => CanMove(1);
    private bool CanReparentSelected() => SelectedNode is { } node && node.Id != Document.Root.Id && !string.IsNullOrWhiteSpace(SelectedParentNodeId) && !ContainsNode(node, SelectedParentNodeId) && FindParent(Document.Root, node.Id)?.Id != SelectedParentNodeId;
    private bool CanMove(int direction)
    {
        var node = SelectedNode;
        var parent = node is null ? null : FindParent(Document.Root, node.Id);
        if (node is null || parent is null) return false;
        var index = Array.FindIndex(parent.Children, child => child.Id == node.Id) + direction;
        return index >= 0 && index < parent.Children.Length;
    }
    private static Object3DColliderDefinition CreateCollider(Object3DColliderKind kind, Object3DColliderDefinition current) => kind switch
    {
        Object3DColliderKind.Sphere => new(kind, current.Center ?? [0, 0, 0], Radius: current.Radius ?? .5, SourceFrame: current.SourceFrame, IsTrigger: current.IsTrigger),
        Object3DColliderKind.Box => new(kind, current.Center ?? [0, 0, 0], Size: current.Size ?? [1, 1, 1], SourceFrame: current.SourceFrame, IsTrigger: current.IsTrigger),
        Object3DColliderKind.Capsule => new(kind, current.Center ?? [0, 0, 0], Radius: current.Radius ?? .5, Height: current.Height ?? 2, Axis: current.Axis ?? Object3DCapsuleAxis.Y, SourceFrame: current.SourceFrame, IsTrigger: current.IsTrigger),
        _ => new(kind, SourceFrame: current.SourceFrame, IsTrigger: current.IsTrigger)
    };
    private static Object3DNodeDefinition ReplaceNode(Object3DNodeDefinition node, string id, Func<Object3DNodeDefinition, Object3DNodeDefinition> edit)
    {
        if (node.Id == id) return edit(node);
        var changed = false;
        var children = node.Children.Select(child => { var replacement = ReplaceNode(child, id, edit); changed |= !ReferenceEquals(child, replacement); return replacement; }).ToArray();
        return changed ? node with { Children = children } : node;
    }
    private static Object3DNodeDefinition? FindNode(Object3DNodeDefinition node, string id) => node.Id == id ? node : node.Children.Select(child => FindNode(child, id)).FirstOrDefault(found => found is not null);
    private static Object3DNodeDefinition? FindParent(Object3DNodeDefinition node, string id)
    {
        foreach (var child in node.Children) { if (child.Id == id) return node; var found = FindParent(child, id); if (found is not null) return found; }
        return null;
    }
    private static bool ContainsNode(Object3DNodeDefinition root, string id) => root.Id == id || root.Children.Any(child => ContainsNode(child, id));
    private static void Flatten(Object3DNodeDefinition node, int depth, ObservableCollection<PrefabHierarchyRow> rows)
    {
        rows.Add(new PrefabHierarchyRow(node.Id, node.Name, depth, node.Components.Select(component => component.Kind).ToArray()));
        foreach (var child in node.Children) Flatten(child, depth + 1, rows);
    }
    private void Notify([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record PrefabHierarchyRow(string Id, string Name, int Depth, string[] Components)
{
    public string IndentedName => new string(' ', Math.Max(0, Depth) * 2) + Name;
    public string ComponentSummary => string.Join(", ", Components.Where(kind => kind != Object3DComponentDefinition.TransformKind));
}
