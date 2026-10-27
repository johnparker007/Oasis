using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private sealed record SceneNodeOption(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record NullableBoolOption(string Label, bool? Value)
    {
        public override string ToString() => Label;
    }

    /// <summary>Renders typed editable fields for the selected node in the active schema 2 Prefab.</summary>
    private void RefreshPrefab3DInspector()
    {
        _inspector.Children.Clear();
        _fields.Clear();

        var document = Prefabs.Document;
        if (document is null)
        {
            Heading("PREFAB");
            _inspector.Children.Add(new TextBlock { Text = "Create or open an Object3D Prefab from the Project pane.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            return;
        }

        var root = document.Root;
        var nodeId = _selectedSceneNodeId ?? root.Id;
        var node = FindNode(root, nodeId) ?? root;
        var location = FindNodeLocation(root, node.Id, parent: null, index: -1);

        Heading($"PREFAB NODE  /  {NodePath(root, node.Id)}");
        Field("Node name", node.Name);
        _inspector.Children.Add(ActionButton("Apply name", () => Try(() => Prefabs.RenameNode(node.Id, _fields["Node name"].Text ?? ""))));

        Heading("LOCAL TRANSFORM");
        var transform = node.Transform;
        Field("Position X", transform.Position.X); Field("Position Y", transform.Position.Y); Field("Position Z", transform.Position.Z);
        Field("Rotation X", transform.Rotation.X); Field("Rotation Y", transform.Rotation.Y); Field("Rotation Z", transform.Rotation.Z);
        Field("Scale X", transform.Scale.X); Field("Scale Y", transform.Scale.Y); Field("Scale Z", transform.Scale.Z);
        _inspector.Children.Add(ActionButton("Apply transform", () => Try(() => Prefabs.SetTransform(node.Id, new MachineObjectTransform(
            new MachineVector3(Number("Position X"), Number("Position Y"), Number("Position Z")),
            new MachineVector3(Number("Rotation X"), Number("Rotation Y"), Number("Rotation Z")),
            new MachineVector3(Number("Scale X"), Number("Scale Y"), Number("Scale Z")))))));

        Heading("HIERARCHY");
        Field("Child name", "New node");
        _inspector.Children.Add(ActionButton("Add child", () => Try(() =>
        {
            var childId = Prefabs.AddNode(node.Id, _fields["Child name"].Text ?? "");
            SelectSceneNode(childId);
        })));
        if (location.Parent is not null)
        {
            _inspector.Children.Add(ActionButton("Remove node", () => Try(() =>
            {
                Prefabs.RemoveNode(node.Id);
                SelectSceneNode(location.Parent.Id);
            })));

            var siblings = location.Parent.Children;
            if (location.Index > 0)
                _inspector.Children.Add(ActionButton("Move up", () => Try(() => ReorderNode(location.Parent, node.Id, -1))));
            if (location.Index + 1 < siblings.Length)
                _inspector.Children.Add(ActionButton("Move down", () => Try(() => ReorderNode(location.Parent, node.Id, 1))));

            var excluded = Descendants(node).Select(child => child.Id).Append(node.Id).ToHashSet(StringComparer.Ordinal);
            var parentOptions = FlattenNodes(root).Where(candidate => !excluded.Contains(candidate.Id))
                .Select(candidate => new SceneNodeOption(candidate.Id, NodePath(root, candidate.Id))).ToArray();
            var parentPicker = new ComboBox
            {
                ItemsSource = parentOptions,
                SelectedItem = parentOptions.FirstOrDefault(option => option.Id == location.Parent.Id)
            };
            AddLabeledControl("New parent", parentPicker);
            _inspector.Children.Add(ActionButton("Reparent node", () => Try(() =>
            {
                if (parentPicker.SelectedItem is not SceneNodeOption selectedParent)
                    throw new ArgumentException("Choose a new parent node.");
                Prefabs.ReparentNode(node.Id, selectedParent.Id);
                SelectSceneNode(node.Id);
            })));
        }
        else
        {
            _inspector.Children.Add(new TextBlock { Text = "The Prefab root stays at the top of the hierarchy and cannot be removed or reparented.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Opacity = .7 });
        }

        Heading("COMPONENTS");
        var modelComponent = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.ModelKind);
        var colliderComponent = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.ColliderKind);
        var rigidbodyComponent = node.Components.FirstOrDefault(component => component.Kind == Object3DComponentDefinition.RigidbodyKind);
        AddModelInspector(node.Id, modelComponent?.Model);
        AddColliderInspector(node.Id, colliderComponent?.Collider);
        AddRigidbodyInspector(node.Id, rigidbodyComponent?.Rigidbody);

        _inspector.Children.Add(new TextBlock
        {
            Text = "Every node has one Transform. Model, Collider and Rigidbody are optional typed components.",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Opacity = .7
        });
    }

    private void AddModelInspector(string nodeId, Object3DModelDefinition? model)
    {
        Heading("MODEL");
        if (model is null)
        {
            _inspector.Children.Add(ActionButton("Add Model component", () => Try(() => Prefabs.AddComponent(nodeId, Object3DComponentDefinition.ModelKind))));
            _inspector.Children.Add(ActionButton("Import GLB…", () => _ = ImportPrefabModelDialogAsync(nodeId)));
            return;
        }

        Field("Model path", model.Path); Field("Model intrinsic scale", model.Scale);
        var upAxis = new ComboBox { ItemsSource = new[] { "X", "Y", "Z" }, SelectedItem = model.UpAxis };
        AddLabeledControl("Model up-axis", upAxis);
        _inspector.Children.Add(ActionButton("Apply Model", () => Try(() => Prefabs.SetModel(nodeId, new Object3DModelDefinition(
            _fields["Model path"].Text ?? "", Number("Model intrinsic scale"), upAxis.SelectedItem as string ?? "Y")))));
        _inspector.Children.Add(ActionButton("Import GLB…", () => _ = ImportPrefabModelDialogAsync(nodeId)));
        _inspector.Children.Add(ActionButton("Remove Model component", () => Try(() => Prefabs.RemoveComponent(nodeId, Object3DComponentDefinition.ModelKind))));
    }

    private void AddColliderInspector(string nodeId, Object3DColliderDefinition? collider)
    {
        Heading("COLLIDER");
        if (collider is null)
        {
            _inspector.Children.Add(ActionButton("Add Collider component", () => Try(() => Prefabs.AddComponent(nodeId, Object3DComponentDefinition.ColliderKind))));
            return;
        }

        var kind = new ComboBox { ItemsSource = Enum.GetNames<Object3DColliderKind>(), SelectedItem = collider.Kind.ToString() };
        AddLabeledControl("Collider kind", kind);
        Field("Collider center X", collider.Center?.ElementAtOrDefault(0) ?? 0);
        Field("Collider center Y", collider.Center?.ElementAtOrDefault(1) ?? 0);
        Field("Collider center Z", collider.Center?.ElementAtOrDefault(2) ?? 0);
        Field("Sphere / capsule radius", collider.Radius ?? .5);
        Field("Box size X", collider.Size?.ElementAtOrDefault(0) ?? 1);
        Field("Box size Y", collider.Size?.ElementAtOrDefault(1) ?? 1);
        Field("Box size Z", collider.Size?.ElementAtOrDefault(2) ?? 1);
        Field("Capsule height", collider.Height ?? 2);
        var axis = new ComboBox { ItemsSource = Enum.GetNames<Object3DCapsuleAxis>(), SelectedItem = collider.Axis?.ToString() ?? Object3DCapsuleAxis.Y.ToString() };
        AddLabeledControl("Capsule axis", axis);
        var sourceFrame = new ComboBox { ItemsSource = Enum.GetNames<Object3DColliderSourceFrame>(), SelectedItem = collider.SourceFrame.ToString() };
        AddLabeledControl("Collider source frame", sourceFrame);
        var isTrigger = new CheckBox { Content = "Is trigger", IsChecked = collider.IsTrigger };
        _inspector.Children.Add(isTrigger);
        _inspector.Children.Add(ActionButton("Apply Collider", () => Try(() =>
        {
            if (kind.SelectedItem is not string kindName || !Enum.TryParse<Object3DColliderKind>(kindName, out var parsedKind))
                throw new ArgumentException("Choose a supported collider kind.");
            if (sourceFrame.SelectedItem is not string frameName || !Enum.TryParse<Object3DColliderSourceFrame>(frameName, out var frame))
                throw new ArgumentException("Choose a supported collider source frame.");
            var center = new[] { Number("Collider center X"), Number("Collider center Y"), Number("Collider center Z") };
            var definition = parsedKind switch
            {
                Object3DColliderKind.None => Object3DColliderDefinition.None,
                Object3DColliderKind.Sphere => new(parsedKind, center, Number("Sphere / capsule radius"), SourceFrame: frame, IsTrigger: isTrigger.IsChecked == true),
                Object3DColliderKind.Box => new(parsedKind, center, Size: [Number("Box size X"), Number("Box size Y"), Number("Box size Z")], SourceFrame: frame, IsTrigger: isTrigger.IsChecked == true),
                Object3DColliderKind.Capsule => new(parsedKind, center, Number("Sphere / capsule radius"), Height: Number("Capsule height"),
                    Axis: axis.SelectedItem is string axisName && Enum.TryParse<Object3DCapsuleAxis>(axisName, out var parsedAxis) ? parsedAxis : Object3DCapsuleAxis.Y,
                    SourceFrame: frame, IsTrigger: isTrigger.IsChecked == true),
                Object3DColliderKind.Mesh => new(parsedKind, SourceFrame: frame, IsTrigger: isTrigger.IsChecked == true),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            Prefabs.SetCollider(nodeId, definition);
        })));
        _inspector.Children.Add(ActionButton("Remove Collider component", () => Try(() => Prefabs.RemoveComponent(nodeId, Object3DComponentDefinition.ColliderKind))));
    }

    private void AddRigidbodyInspector(string nodeId, Object3DRigidbodyDefinition? rigidbody)
    {
        Heading("RIGIDBODY");
        if (rigidbody is null)
        {
            _inspector.Children.Add(ActionButton("Add Rigidbody component", () => Try(() => Prefabs.AddComponent(nodeId, Object3DComponentDefinition.RigidbodyKind))));
            return;
        }

        var mode = new ComboBox { ItemsSource = Enum.GetNames<Object3DRigidbodyMode>(), SelectedItem = rigidbody.Mode.ToString() };
        AddLabeledControl("Rigidbody mode", mode);
        Field("Rigidbody mass", rigidbody.Mass ?? 1);
        var gravityOptions = new[]
        {
            new NullableBoolOption("Default", null),
            new NullableBoolOption("Disabled", false),
            new NullableBoolOption("Enabled", true)
        };
        var gravity = new ComboBox
        {
            ItemsSource = gravityOptions,
            SelectedItem = gravityOptions.First(option => option.Value == rigidbody.UseGravity)
        };
        AddLabeledControl("Use gravity", gravity);
        _inspector.Children.Add(ActionButton("Apply Rigidbody", () => Try(() =>
        {
            if (mode.SelectedItem is not string modeName || !Enum.TryParse<Object3DRigidbodyMode>(modeName, out var parsedMode))
                throw new ArgumentException("Choose a supported Rigidbody mode.");
            var mass = parsedMode == Object3DRigidbodyMode.Dynamic ? Number("Rigidbody mass") : null;
            var useGravity = (gravity.SelectedItem as NullableBoolOption)?.Value;
            Prefabs.SetRigidbody(nodeId, new Object3DRigidbodyDefinition(parsedMode, mass, useGravity));
        })));
        _inspector.Children.Add(ActionButton("Remove Rigidbody component", () => Try(() => Prefabs.RemoveComponent(nodeId, Object3DComponentDefinition.RigidbodyKind))));
    }

    private void ReorderNode(Object3DNodeDefinition parent, string nodeId, int delta)
    {
        var children = parent.Children.Select(child => child.Id).ToList();
        var index = children.IndexOf(nodeId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= children.Count) return;
        (children[index], children[target]) = (children[target], children[index]);
        Prefabs.ReorderChildren(parent.Id, children);
        SelectSceneNode(nodeId);
    }

    private static Object3DNodeDefinition? FindNode(Object3DNodeDefinition node, string id)
    {
        if (node.Id == id) return node;
        foreach (var child in node.Children)
            if (FindNode(child, id) is { } found) return found;
        return null;
    }

    private static (Object3DNodeDefinition? Parent, int Index) FindNodeLocation(Object3DNodeDefinition node, string id,
        Object3DNodeDefinition? parent, int index)
    {
        if (node.Id == id) return (parent, index);
        for (var childIndex = 0; childIndex < node.Children.Length; childIndex++)
        {
            var result = FindNodeLocation(node.Children[childIndex], id, node, childIndex);
            if (result.Parent is not null || node.Children[childIndex].Id == id) return result;
        }
        return (null, -1);
    }

    private static IReadOnlyList<Object3DNodeDefinition> FlattenNodes(Object3DNodeDefinition root)
    {
        var result = new List<Object3DNodeDefinition>();
        void Visit(Object3DNodeDefinition node)
        {
            result.Add(node);
            foreach (var child in node.Children) Visit(child);
        }
        Visit(root);
        return result;
    }

    private static IEnumerable<Object3DNodeDefinition> Descendants(Object3DNodeDefinition node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static string NodePath(Object3DNodeDefinition root, string id)
    {
        var parts = new List<string>();
        bool Visit(Object3DNodeDefinition node)
        {
            parts.Add(node.Name);
            if (node.Id == id) return true;
            foreach (var child in node.Children)
                if (Visit(child)) return true;
            parts.RemoveAt(parts.Count - 1);
            return false;
        }
        _ = Visit(root);
        return string.Join(" / ", parts);
    }
}
