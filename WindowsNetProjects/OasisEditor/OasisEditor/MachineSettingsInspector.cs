using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Oasis.Scripting;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    partial void AddMachineWorkflowMenus(MenuItem fileMenu, MenuItem createMenu)
    {
        MenuAction(fileMenu, "Open Machine…", async () => await OpenMachineDialog());
        MenuAction(fileMenu, "Create Machine…", async () => await NewMachineDialog());
        var item = new MenuItem { Header = "Machine settings…" };
        item.Click += async (_, _) => await NewMachineDialog();
        createMenu.Items.Add(item);
        AddMachineBuildExportMenus(fileMenu);
    }

    private async Task OpenMachineDialog()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Open Machine", FileTypeFilter = [new FilePickerFileType("Oasis Machine") { Patterns = ["asset.machine"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
        {
            try { Machines.OpenMachine(path); ShowMachine(); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { SetStatus(e.Message); }
        }
    }

    private async Task NewMachineDialog()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var name = new TextBox { PlaceholderText = "Machine name" };
        var message = new TextBlock { Text = "Create a native schema-11 Machine in the open project.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new global::Avalonia.Thickness(20), Spacing = 12 };
        panel.Children.Add(message); panel.Children.Add(name);
        var dialog = new Window { Title = "New Machine", Width = 420, Height = 210, Content = panel, CanResize = false };
        panel.Children.Add(TextEntryDialog.Actions(dialog, name, ActionButton("Create Machine", () =>
        {
            try { Machines.CreateMachine(name.Text ?? string.Empty); ShowMachine(); dialog.Close(); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { message.Text = e.Message; }
        })));
        await dialog.ShowDialog(owner);
    }

    private void RefreshMachineInspector()
    {
        _inspected = Machines.MachinePath;
        _inspector.Children.Clear(); _fields.Clear();
        var machine = Machines.Document;
        if (machine is null)
        {
            Heading("MACHINE"); _inspector.Children.Add(new TextBlock { Text = "Create or open a Machine from the Project pane.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
            return;
        }

        Heading("MACHINE"); Field("Display name", machine.DisplayName);
        AddLabeledControl("Runtime", new TextBlock { Text = machine.Runtime.Kind, VerticalAlignment = VerticalAlignment.Center });
        var source = new TextBox { AcceptsReturn = true, MinHeight = 140, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Text = Machines.BehaviorSource };
        AddLabeledControl("Oasis behavior source", source);
        var diagnostics = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        _inspector.Children.Add(diagnostics);
        _inspector.Children.Add(ActionButton("Check Oasis script", () =>
        {
            var result = OasisScriptCompiler.Compile(source.Text ?? string.Empty, MachineBehaviorDefinition.CanonicalSourcePath);
            diagnostics.Text = result.Success ? "Script syntax and semantics are valid." : string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString()));
        }));
        _inspector.Children.Add(ActionButton("Apply Oasis behavior", () => Try(() =>
        {
            var script = source.Text ?? string.Empty;
            var result = OasisScriptCompiler.Compile(script, MachineBehaviorDefinition.CanonicalSourcePath);
            if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
            Machines.SetOasisRuntime(_fields["Display name"].Text?.Trim() ?? machine.DisplayName, script);
        })));
        var emulation = machine.Runtime as EmulationRuntimeDefinition;
        var selectedPlatformAtOpen = emulation?.Platform ?? FruitMachinePlatformType.Impact;
        var platform = new ComboBox { ItemsSource = EmulationRuntimePlatforms.Supported.Select(p => p.ToString()).ToArray(), SelectedItem = selectedPlatformAtOpen.ToString() };
        AddLabeledControl("Emulation platform", platform);
        Heading("PLATFORM SETTINGS");
        var platformSettings = emulation?.PlatformSettings ?? EmulationRuntimeDefinition.Create(selectedPlatformAtOpen).PlatformSettings;
        var settingControls = BuildTypedFields(platformSettings, 0);
        foreach (var control in settingControls.Controls) _inspector.Children.Add(control);
        _inspector.Children.Add(ActionButton("Apply emulation settings", () => Try(() =>
        {
            var selectedPlatform = platform.SelectedItem is string platformName && Enum.TryParse<FruitMachinePlatformType>(platformName, out var selected) ? selected : selectedPlatformAtOpen;
            object settingsCopy;
            if (selectedPlatform == selectedPlatformAtOpen)
            {
                settingsCopy = Clone(platformSettings);
                settingControls.Apply(settingsCopy);
            }
            else settingsCopy = EmulationRuntimeDefinition.Create(selectedPlatform).PlatformSettings;
            var nextRuntime = new EmulationRuntimeDefinition(selectedPlatform, settingsCopy);
            Machines.UpdateDocument(current => current with { DisplayName = _fields["Display name"].Text?.Trim() ?? current.DisplayName, Runtime = nextRuntime, Behavior = null });
        })));

        Heading("INTERACTION");
        Field("Interaction kind", machine.Interaction.Kind);
        Field("Cue prefab alias", machine.Interaction.CuePrefabAlias ?? "");
        Field("Playing surface ID", machine.Interaction.PlayingSurfaceId ?? "");
        _inspector.Children.Add(ActionButton("Apply interaction", () => Try(() => Machines.SetInteraction(
            string.Equals(_fields["Interaction kind"].Text, "Pool", StringComparison.OrdinalIgnoreCase)
                ? new MachineInteractionDefinition("Pool", _fields.GetValueOrDefault("Cue prefab alias")?.Text, _fields.GetValueOrDefault("Playing surface ID")?.Text)
                : new MachineInteractionDefinition("Buttons")))));

        Heading($"INPUT DEFINITIONS ({machine.InputDefinitions.Count})");
        for (var index = 0; index < machine.InputDefinitions.Count; index++)
        {
            var input = machine.InputDefinitions[index];
            Heading($"INPUT {index + 1}  /  {input.Name}");
            Field($"Input {index} name", input.Name); Field($"Input {index} kind", input.Kind.ToString());
            Field($"Input {index} button", input.ButtonNumber); Field($"Input {index} keyboard shortcut", input.KeyboardShortcut);
            Field($"Input {index} MFME shortcut", input.RawMfmeShortcut); Field($"Input {index} notes", input.Notes);
            Field($"Input {index} visual element GUID", input.LinkedVisualElementId?.ToString("D") ?? "");
            Field($"Input {index} coin channel", input.CoinChannel?.ToString(CultureInfo.InvariantCulture) ?? "");
            Field($"Input {index} coin value", input.CoinValue?.ToString(CultureInfo.InvariantCulture) ?? "");
            var inverted = new CheckBox { Content = "Invert input", IsChecked = input.Inverted }; _inspector.Children.Add(inverted);
            var coin = new CheckBox { Content = "Coin input", IsChecked = input.CoinInput }; _inspector.Children.Add(coin);
            _inspector.Children.Add(ActionButton($"Apply input {index + 1}", () => Try(() =>
            {
                var updated = machine.InputDefinitions.Select(CloneInput).ToList();
                updated[index].Name = _fields[$"Input {index} name"].Text ?? "";
                if (!Enum.TryParse<InputDefinitionKind>(_fields[$"Input {index} kind"].Text, out var kind)) throw new ArgumentException("Input kind is not recognized.");
                updated[index].Kind = kind;
                updated[index].ButtonNumber = _fields[$"Input {index} button"].Text ?? "";
                updated[index].KeyboardShortcut = _fields[$"Input {index} keyboard shortcut"].Text ?? "";
                updated[index].RawMfmeShortcut = _fields[$"Input {index} MFME shortcut"].Text ?? "";
                updated[index].Notes = _fields[$"Input {index} notes"].Text ?? "";
                updated[index].LinkedVisualElementId = ParseNullableGuid(_fields[$"Input {index} visual element GUID"].Text);
                updated[index].CoinInput = coin.IsChecked == true; updated[index].Inverted = inverted.IsChecked == true;
                updated[index].CoinChannel = ParseNullableInt(_fields[$"Input {index} coin channel"].Text, "Coin channel");
                updated[index].CoinValue = ParseNullableInt(_fields[$"Input {index} coin value"].Text, "Coin value");
                Machines.SetInputs(updated);
            })));
            _inspector.Children.Add(ActionButton($"Remove input {index + 1}", () => Try(() => Machines.SetInputs(machine.InputDefinitions.Where((_, i) => i != index)))));
        }
        _inspector.Children.Add(ActionButton("Add input definition", () => Try(() =>
        {
            var inputs = machine.InputDefinitions.Select(CloneInput).ToList();
            inputs.Add(new InputDefinitionModel { Id = "input-" + Guid.NewGuid().ToString("N"), Name = "New input", Kind = InputDefinitionKind.Button });
            Machines.SetInputs(inputs);
        })));
        var recovered = _session.ImportedInputDefinitions;
        if (recovered.Count > 0)
            _inspector.Children.Add(ActionButton($"Apply {recovered.Count} recovered FML inputs", () => Try(() => Machines.ApplyRecoveredInputs(recovered))));

        _inspector.Children.Add(CreateMachineInputDiagnosticsControl());
        Heading("ASSET ASSIGNMENTS");
        var cabinetAsset = ReusableAssetCombo(EditorAssetType.Cabinet3D, machine.CabinetAsset);
        AddLabeledControl("Cabinet asset", cabinetAsset);
        _inspector.Children.Add(ActionButton("Apply cabinet assignment", () => Try(() => Machines.UpdateDocument(current => current with
        { CabinetAsset = SelectedReusableAsset(cabinetAsset) }))));

        Heading("FACE SURFACE ASSIGNMENTS");
        for (var i = 0; i < machine.SurfaceAssignments.Length; i++)
        {
            var assignment = machine.SurfaceAssignments[i]; Field($"Face target {i}", assignment.TargetId); Field($"Face asset {i}", assignment.FaceAssetPath);
        }
        _inspector.Children.Add(ActionButton("Apply Face assignments", () => Try(() =>
        {
            var assignments = machine.SurfaceAssignments.Select((x, i) => new MachineSurfaceAssignment(_fields[$"Face target {i}"].Text ?? "", _fields[$"Face asset {i}"].Text ?? "")).ToArray();
            Machines.UpdateDocument(current => current with { SurfaceAssignments = assignments });
        })));
        _inspector.Children.Add(ActionButton("Add Face assignment", () => Try(() =>
        {
            var asset = RequireProjectAsset("Face", "Faces");
            Machines.UpdateDocument(current => current with { SurfaceAssignments = [.. current.SurfaceAssignments, new MachineSurfaceAssignment(Guid.NewGuid().ToString("D"), asset.ManifestPath)] });
        })));

        Heading("REEL ASSET ASSIGNMENTS");
        foreach (var reference in LogicalReelReferences(machine))
        {
            var current = machine.ReelAssignments.FirstOrDefault(a => a.MachineReelReference == reference)?.ReelAsset;
            var reelAsset = ReusableAssetCombo(EditorAssetType.Reel, current);
            AddLabeledControl($"{reference.Kind}:{reference.Id}", reelAsset);
            _inspector.Children.Add(ActionButton($"Apply {reference.Kind}:{reference.Id}", () => Try(() => AssignMachineReel(reference, SelectedReusableAsset(reelAsset)))));
            _inspector.Children.Add(ActionButton($"Remove {reference.Kind}:{reference.Id}", () => Try(() => AssignMachineReel(reference, null))));
        }
        var newReelReference = new TextBox { PlaceholderText = "Enter a logical reference, for example reel:0" };
        AddLabeledControl("New logical Reel reference", newReelReference);
        var newReelAsset = ReusableAssetCombo(EditorAssetType.Reel);
        newReelAsset.SelectedItem = (newReelAsset.ItemsSource as IEnumerable<ReusableAssetChoice>)?.FirstOrDefault(choice => choice.Reference is not null);
        AddLabeledControl("New Reel asset", newReelAsset);
        _inspector.Children.Add(ActionButton("Add Reel assignment", () => Try(() =>
        {
            if (!MachineObjectReference.TryParse(newReelReference.Text, out var reference) || reference.Kind != MachineObjectKind.Reel)
                throw new ArgumentException("Enter a logical Reel reference such as reel:0.");
            var asset = SelectedReusableAsset(newReelAsset) ?? throw new InvalidOperationException("Choose a Reel asset to create the assignment.");
            AssignMachineReel(reference, asset);
        })));

        Heading("PREFAB DECLARATIONS");
        for (var i = 0; i < machine.PrefabDependencies.Length; i++)
        {
            var index = i;
            var declaration = machine.PrefabDependencies[index];
            var alias = new TextBox { Text = declaration.Alias };
            AddLabeledControl($"Prefab alias {index}", alias);
            var prefabAsset = ReusableAssetCombo(EditorAssetType.Object3D, declaration.PrefabAsset);
            AddLabeledControl($"Prefab asset {index}", prefabAsset);
            _inspector.Children.Add(ActionButton($"Apply Prefab declaration {index + 1}", () => Try(() =>
            {
                var selected = SelectedReusableAsset(prefabAsset) ?? throw new InvalidOperationException("Choose a Prefab asset or remove this declaration.");
                var updatedAlias = string.IsNullOrWhiteSpace(alias.Text) ? throw new ArgumentException("Prefab alias is required.") : alias.Text.Trim();
                Machines.UpdateDocument(current => current with
                {
                    PrefabDependencies = current.PrefabDependencies.Select((item, itemIndex) => itemIndex == index
                        ? new MachinePrefabDeclaration(updatedAlias, selected) : item).ToArray()
                });
            })));
            _inspector.Children.Add(ActionButton($"Remove Prefab declaration {index + 1}", () => Try(() =>
                Machines.UpdateDocument(current => current with { PrefabDependencies = current.PrefabDependencies.Where((_, itemIndex) => itemIndex != index).ToArray() }))));
        }
        var newPrefabAlias = new TextBox { PlaceholderText = "Prefab alias" };
        AddLabeledControl("New Prefab alias", newPrefabAlias);
        var newPrefabAsset = ReusableAssetCombo(EditorAssetType.Object3D);
        newPrefabAsset.SelectedItem = (newPrefabAsset.ItemsSource as IEnumerable<ReusableAssetChoice>)?.FirstOrDefault(choice => choice.Reference is not null);
        AddLabeledControl("New Prefab asset", newPrefabAsset);
        _inspector.Children.Add(ActionButton("Add Prefab declaration", () => Try(() =>
        {
            var alias = string.IsNullOrWhiteSpace(newPrefabAlias.Text) ? throw new ArgumentException("Enter a Prefab alias.") : newPrefabAlias.Text.Trim();
            var asset = SelectedReusableAsset(newPrefabAsset) ?? throw new InvalidOperationException("Choose a Prefab asset.");
            Machines.UpdateDocument(current => current with { PrefabDependencies = [.. current.PrefabDependencies, new MachinePrefabDeclaration(alias, asset)] });
        })));

        Heading("MACHINE ANCHORS");
        for (var i = 0; i < machine.Anchors.Length; i++)
        {
            var anchor = machine.Anchors[i]; Field($"Anchor ID {i}", anchor.Id); Field($"Anchor name {i}", anchor.DisplayName);
            Field($"Anchor X {i}", anchor.Position.X); Field($"Anchor Y {i}", anchor.Position.Y); Field($"Anchor Z {i}", anchor.Position.Z);
            Field($"Anchor rotation X {i}", anchor.Rotation.X); Field($"Anchor rotation Y {i}", anchor.Rotation.Y); Field($"Anchor rotation Z {i}", anchor.Rotation.Z);
        }
        _inspector.Children.Add(ActionButton("Apply anchors", () => Try(() =>
        {
            var anchors = machine.Anchors.Select((x, i) => x with
            {
                Id = _fields[$"Anchor ID {i}"].Text ?? "", DisplayName = _fields[$"Anchor name {i}"].Text ?? "",
                Position = new MachineVector3(Number($"Anchor X {i}"), Number($"Anchor Y {i}"), Number($"Anchor Z {i}")),
                Rotation = new MachineVector3(Number($"Anchor rotation X {i}"), Number($"Anchor rotation Y {i}"), Number($"Anchor rotation Z {i}"))
            }).ToArray();
            Machines.UpdateDocument(current => current with { Anchors = anchors });
        })));
        _inspector.Children.Add(ActionButton("Add anchor", () => Try(() => Machines.UpdateDocument(current => current with { Anchors = [.. current.Anchors, MachineAnchor.Create("anchor-" + Guid.NewGuid().ToString("N"))] }))));

        Heading("GEOMETRY SURFACES");
        for (var i = 0; i < machine.GeometrySurfaces.Length; i++)
        {
            var surface = machine.GeometrySurfaces[i]; Field($"Surface ID {i}", surface.Id); Field($"Surface width {i}", surface.Width); Field($"Surface length {i}", surface.Length);
            Field($"Surface X {i}", surface.Position.X); Field($"Surface Y {i}", surface.Position.Y); Field($"Surface Z {i}", surface.Position.Z);
            Field($"Surface rotation X {i}", surface.Rotation.X); Field($"Surface rotation Y {i}", surface.Rotation.Y); Field($"Surface rotation Z {i}", surface.Rotation.Z);
        }
        _inspector.Children.Add(ActionButton("Apply geometry surfaces", () => Try(() =>
        {
            var surfaces = machine.GeometrySurfaces.Select((x, i) => x with
            {
                Id = _fields[$"Surface ID {i}"].Text ?? "", Width = Number($"Surface width {i}"), Length = Number($"Surface length {i}"),
                Position = new MachineVector3(Number($"Surface X {i}"), Number($"Surface Y {i}"), Number($"Surface Z {i}")),
                Rotation = new MachineVector3(Number($"Surface rotation X {i}"), Number($"Surface rotation Y {i}"), Number($"Surface rotation Z {i}"))
            }).ToArray();
            Machines.UpdateDocument(current => current with { GeometrySurfaces = surfaces });
        })));
        _inspector.Children.Add(ActionButton("Add geometry surface", () => Try(() => Machines.UpdateDocument(current => current with { GeometrySurfaces = [.. current.GeometrySurfaces, new MachineGeometrySurface("surface-" + Guid.NewGuid().ToString("N"), MachineVector3.Zero, MachineVector3.Zero, 1, 1)] }))));
        _inspector.Children.Add(new TextBlock { Text = "Use the 3D Scene to inspect placements and the Inspector to edit; package references and authored declarations are available here.", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Opacity = .7 });
    }

    private AssetSummary RequireProjectAsset(string type, string friendlyName)
    {
        var asset = Machines.ListProjectAssets().Where(candidate => string.Equals(candidate.Type, type, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        return asset ?? throw new InvalidOperationException($"No registered {friendlyName} asset exists in this project. Create or import one before adding this assignment.");
    }

    private ComboBox ReusableAssetCombo(EditorAssetType type, AssetReference? current = null)
    {
        var choices = GetReusableAssetChoices(type, current);
        var combo = new ComboBox
        {
            ItemsSource = choices,
            SelectedItem = choices.FirstOrDefault(choice => choice.Reference == current) ?? choices.FirstOrDefault(),
            MinWidth = 220
        };
        combo.DropDownOpened += (_, _) =>
        {
            var selected = combo.SelectedItem is ReusableAssetChoice choice ? choice.Reference : current;
            var refreshed = GetReusableAssetChoices(type, selected);
            combo.ItemsSource = refreshed;
            combo.SelectedItem = refreshed.FirstOrDefault(item => item.Reference == selected) ?? refreshed.FirstOrDefault();
        };
        return combo;
    }

    private static AssetReference? SelectedReusableAsset(ComboBox combo)
        => (combo.SelectedItem as ReusableAssetChoice)?.Reference;

    private IReadOnlyList<MachineObjectReference> LogicalReelReferences(MachineDocument machine)
    {
        var references = machine.ReelAssignments.Select(assignment => assignment.MachineReelReference)
            .Where(reference => reference.Kind == MachineObjectKind.Reel && !reference.IsEmpty).ToHashSet();
        foreach (var assignment in machine.SurfaceAssignments)
        {
            try
            {
                var manifest = _session.ResolveAsset(assignment.FaceAssetPath);
                if (!FaceDocumentStorage.TryReadValidated(File.ReadAllText(manifest), out var face, out _)) continue;
                foreach (var element in face.Elements ?? [])
                    if (string.Equals(element.Kind, "reelMount", StringComparison.OrdinalIgnoreCase)
                        && MachineObjectReference.TryParse(element.LinkedMachineObjectReference, out var reference)
                        && reference.Kind == MachineObjectKind.Reel)
                        references.Add(reference);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { }
        }
        return references.OrderBy(reference => reference.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void AddLabeledControl(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("145,*"), ColumnSpacing = 8 };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        Grid.SetColumn(control, 1); row.Children.Add(control); _inspector.Children.Add(row);
    }

    private (List<Control> Controls, Action<object> Apply) BuildTypedFields(object source, int depth)
    {
        var controls = new List<Control>();
        var appliers = new List<Action<object>>();
        if (depth > 4) return (controls, _ => { });
        foreach (var property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is null))
        {
            var value = property.GetValue(source);
            if (value is null) continue;
            if (TryControl(value, property.PropertyType, out var editor))
            {
                controls.Add(EditorRow(NiceName(property.Name), editor));
                appliers.Add(target => property.SetValue(target, ReadControl(editor, property.PropertyType)));
            }
            else if (value is System.Collections.IList list)
            {
                var group = new StackPanel { Spacing = 4, Margin = new global::Avalonia.Thickness(6, 2) };
                group.Children.Add(new TextBlock { Text = NiceName(property.Name), FontWeight = global::Avalonia.Media.FontWeight.SemiBold });
                for (var i = 0; i < list.Count; i++)
                {
                    var itemIndex = i;
                    var item = list[i]; if (item is null) continue;
                    if (TryControl(item, item.GetType(), out var itemEditor))
                    {
                        group.Children.Add(EditorRow($"{itemIndex + 1}", itemEditor)); var itemType = item.GetType();
                        appliers.Add(target => ((System.Collections.IList)property.GetValue(target)!)[itemIndex] = ReadControl(itemEditor, itemType));
                    }
                    else if (!item.GetType().IsPrimitive && item is not string)
                    {
                        var nested = BuildTypedFields(item, depth + 1);
                        group.Children.Add(new TextBlock { Text = $"{NiceName(property.Name)} {itemIndex + 1}", Opacity = .7 });
                        group.Children.AddRange(nested.Controls);
                        appliers.Add(target => { var items = (System.Collections.IList)property.GetValue(target)!; if (items[itemIndex] is { } nestedTarget) nested.Apply(nestedTarget); });
                    }
                }
                controls.Add(group);
            }
        }
        return (controls, target => { foreach (var apply in appliers) apply(target); });
    }

    private static Control EditorRow(string label, Control editor)
    {
        editor.Name = "MachineSetting_" + string.Concat(label.Where(char.IsLetterOrDigit));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("125,*"), ColumnSpacing = 6 };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        Grid.SetColumn(editor, 1); row.Children.Add(editor); return row;
    }

    private static bool TryControl(object value, Type type, out Control control)
    {
        if (type == typeof(bool)) { control = new CheckBox { IsChecked = (bool)value }; return true; }
        if (type.IsEnum) { control = new ComboBox { ItemsSource = Enum.GetNames(type), SelectedItem = value.ToString() }; return true; }
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        { control = new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) }; return true; }
        control = null!; return false;
    }

    private static object ReadControl(Control control, Type type)
    {
        if (type == typeof(bool)) return ((CheckBox)control).IsChecked == true;
        if (type.IsEnum) return Enum.Parse(type, ((ComboBox)control).SelectedItem?.ToString() ?? throw new ArgumentException("Select a value."));
        var text = ((TextBox)control).Text ?? "";
        if (type == typeof(string)) return text;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) || !double.IsFinite(numeric))
            throw new ArgumentException($"Enter a finite value for {type.Name}.");
        return Convert.ChangeType(numeric, type, CultureInfo.InvariantCulture);
    }

    private static object Clone(object value) => JsonSerializer.Deserialize(JsonSerializer.Serialize(value, value.GetType()), value.GetType())!;
    private static string NiceName(string name) => string.Concat(name.Select((c, i) => i > 0 && (char.IsUpper(c) || (char.IsDigit(c) && char.IsLetter(name[i - 1]))) ? " " + c : c.ToString()));
    private static int? ParseNullableInt(string? text, string label) => string.IsNullOrWhiteSpace(text) ? null : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw new ArgumentException($"{label} must be a whole number.");
    private static Guid? ParseNullableGuid(string? text) => string.IsNullOrWhiteSpace(text) ? null : Guid.TryParse(text, out var value) ? value : throw new ArgumentException("Visual element ID must be a GUID.");
    private static InputDefinitionModel CloneInput(InputDefinitionModel input) => new()
    {
        Id = input.Id, Name = input.Name, Kind = input.Kind, ButtonNumber = input.ButtonNumber, CoinInput = input.CoinInput,
        CoinChannel = input.CoinChannel, CoinValue = input.CoinValue, Inverted = input.Inverted,
        RawMfmeShortcut = input.RawMfmeShortcut, KeyboardShortcut = input.KeyboardShortcut,
        LinkedVisualElementId = input.LinkedVisualElementId, Notes = input.Notes
    };
}
