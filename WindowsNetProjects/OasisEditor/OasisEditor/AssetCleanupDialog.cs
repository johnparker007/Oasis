using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace OasisEditor.Avalonia;

internal sealed class CleanupChoice(string name, string path, UnreferencedAsset? candidate = null) : INotifyPropertyChanged
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public UnreferencedAsset? Candidate { get; } = candidate;
    public List<CleanupChoice> Children { get; } = [];
    public CleanupChoice? Parent { get; set; }
    public Action? SelectionChanged { get; set; }
    private bool? _checked = true;
    public bool? IsChecked
    {
        get => _checked;
        set
        {
            if (value is null || _checked == value) return;
            SetDescendants(value.Value); Parent?.UpdateParent(); SelectionChanged?.Invoke();
        }
    }
    private void SetDescendants(bool value)
    { _checked = value; PropertyChanged?.Invoke(this, new(nameof(IsChecked))); foreach (var child in Children) child.SetDescendants(value); }
    private void UpdateParent()
    {
        _checked = Children.All(c => c._checked == true) ? true : Children.All(c => c._checked == false) ? false : null;
        PropertyChanged?.Invoke(this, new(nameof(IsChecked))); Parent?.UpdateParent();
    }
    public IEnumerable<CleanupChoice> All() => new[] { this }.Concat(Children.SelectMany(c => c.All()));
    public event PropertyChangedEventHandler? PropertyChanged;
}

internal static class AssetCleanupDialog
{
    internal static CleanupChoice BuildChoices(AssetCleanupPlan plan)
    {
        var root = new CleanupChoice("Project assets", "");
        foreach (var candidate in plan.Candidates)
        {
            var parts = candidate.Path.Split('/'); var parent = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var path = string.Join('/', parts.Take(i + 1));
                var child = parent.Children.FirstOrDefault(c => c.Path == path);
                if (child is null)
                {
                    var leaf = i == parts.Length - 1;
                    var label = parts[i] + (leaf ? candidate.IsDirectory ? " (empty folder)" : $"  ({candidate.Bytes:N0} bytes)" : "");
                    child = new CleanupChoice(label, path, leaf ? candidate : null) { Parent = parent };
                    parent.Children.Add(child);
                }
                parent = child;
            }
        }
        return root;
    }

    public static async Task ShowAsync(Window owner, AssetCleanupPlan plan, Func<string[], AssetCleanupResult> delete)
    {
        var choices = BuildChoices(plan);
        var window = new Window { Name = "AssetCleanupDialog", Title = "Delete unreferenced assets", Width = 760, Height = 560,
            MinWidth = 560, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var content = new Grid { Margin = new Thickness(18), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), RowSpacing = 12 };
        var description = new TextBlock { Text = "Review unused assets before deleting. Uncheck any files or folders you want to keep.\nSaved asset packages and open undo/redo history are protected. Only loose project media and unused FML output are listed; scripts, ROMs, Library and Generated files are kept.", TextWrapping = TextWrapping.Wrap };
        content.Children.Add(description);
        var tree = new TreeView { Name = "AssetCleanupChoices", ItemsSource = choices.Children,
            ItemTemplate = new FuncTreeDataTemplate<CleanupChoice>((node, _) =>
            {
                var check = new CheckBox { Content = node!.Name };
                check.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(CleanupChoice.IsChecked)) { Source = node, Mode = BindingMode.TwoWay });
                ToolTip.SetTip(check, node.Path); global::Avalonia.Automation.AutomationProperties.SetName(check, node.Path);
                return check;
            }, node => node!.Children) };
        tree.Styles.Add(new Style(s => s.OfType<TreeViewItem>()) { Setters = { new Setter(TreeViewItem.IsExpandedProperty, true) } });
        Grid.SetRow(tree, 1); content.Children.Add(tree);
        var message = new TextBlock { Name = "AssetCleanupMessage", TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(message, 2); content.Children.Add(message);
        var confirm = new Button { Name = "DeleteSelectedAssets", Content = "Delete Selected" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => window.Close();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { confirm, cancel } };
        Grid.SetRow(actions, 3); content.Children.Add(actions);
        string[] Selected() => choices.All().Where(n => n.Candidate is not null && n.IsChecked == true).Select(n => n.Path).ToArray();
        var blocked = false;
        void Update()
        {
            if (blocked) return;
            var selected = Selected(); confirm.IsEnabled = selected.Length > 0;
            message.Text = plan.Candidates.Count == 0 ? "No unreferenced assets were found." : $"{selected.Length} of {plan.Candidates.Count} items selected.";
        }
        foreach (var choice in choices.All()) choice.SelectionChanged = Update;
        Update();
        confirm.Click += (_, _) =>
        {
            try
            {
                var result = delete(Selected());
                if (result.Failures.Count == 0) { window.Close(); return; }
                message.Text = $"Deleted {result.Deleted.Count} items. The following could not be deleted:\n" + string.Join('\n', result.Failures);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or System.Xml.XmlException)
            { message.Text = ex.Message; }
            blocked = true; confirm.IsEnabled = false; cancel.Content = "Close";
        };
        window.Content = content; await window.ShowDialog(owner);
    }
}
