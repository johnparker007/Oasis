using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private TextBox? _hierarchyRename;

    private static bool IsHierarchyBackground(object? source) => source is Visual visual &&
        !visual.GetVisualAncestors().Prepend(visual).Any(v => v is ListBoxItem or ScrollBar);

    private void InitializeHierarchyBackground()
    {
        _hierarchy.ContextRequested += (_, args) =>
        {
            if (_faceActive || _machineActive || !IsHierarchyBackground(args.Source)) return;
            var menu = new ContextMenu();
            var create = new MenuItem { Header = "Create Group" };
            create.Click += (_, _) => Try(() => CreatePanelGroupFromUi(false));
            menu.Items.Add(create);
            args.Handled = true;
            _hierarchy.ContextMenu = menu;
            menu.Closed += (_, _) => { if (ReferenceEquals(_hierarchy.ContextMenu, menu)) _hierarchy.ContextMenu = null; };
            menu.Open(_hierarchy);
        };
        DragDrop.SetAllowDrop(_hierarchy, true);
        _hierarchy.AddHandler(DragDrop.DragOverEvent, (_, args) =>
        {
            if (_faceActive || !IsHierarchyBackground(args.Source)) return;
            args.DragEffects = ReferenceEquals(args.DataTransfer.TryGetValue(GroupDragFormat), this) ? DragDropEffects.Move : DragDropEffects.None;
            args.Handled = true;
        });
        _hierarchy.AddHandler(DragDrop.DropEvent, (_, args) =>
        {
            if (_faceActive || !IsHierarchyBackground(args.Source) || !ReferenceEquals(args.DataTransfer.TryGetValue(GroupDragFormat), this)) return;
            args.Handled = true;
            Try(() => _session.ReparentSelection(null));
        });
    }

    private void BeginHierarchyRename(string id)
    {
        // Context menus close after their command runs; focus the editor afterwards.
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !SelectedIds.Contains(id)) return;
            _hierarchy.UpdateLayout();
            var row = _hierarchy.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => Equals(p.Tag, id));
            var slot = row?.Children.OfType<ContentControl>().FirstOrDefault(c => c.Name == "HierarchyLabel");
            var item = ActiveElements.FirstOrDefault(e => e.Id == id);
            if (slot is null || item is null) return;
            var previous = slot.Content;
            var name = !_faceActive && item.Kind == "group" ? _session.Groups.Single(g => g.Id == id) : null;
            var input = new TextBox { Name = "HierarchyRename", Text = name is null ? item.Name : name.AutomaticName ? "" : name.Name,
                MinWidth = 100, MinHeight = 20, Padding = new Thickness(2, 0), PlaceholderText = item.Name };
            global::Avalonia.Automation.AutomationProperties.SetName(input, "Rename " + item.Name);
            _hierarchyRename = input; slot.Content = input;
            bool finished = false;
            void Finish(bool save)
            {
                if (finished) return;
                if (save && string.IsNullOrWhiteSpace(input.Text) && name is null)
                { SetStatus("Component name cannot be blank."); return; }
                finished = true; _hierarchyRename = null; slot.Content = previous;
                if (save) Try(() =>
                {
                    if (_faceActive) _faces.UpdateElement(id, name: input.Text);
                    else if (name is not null) _session.UpdateGroup(id, name: string.IsNullOrWhiteSpace(input.Text) ? null : input.Text, automaticName: string.IsNullOrWhiteSpace(input.Text));
                    else _session.UpdateElement(id, name: input.Text);
                });
                _hierarchy.Focusable = true; _hierarchy.Focus();
            }
            input.KeyDown += (_, args) =>
            {
                if (args.Key is not (Key.Enter or Key.Escape)) return;
                args.Handled = true; Finish(args.Key == Key.Enter);
            };
            input.LostFocus += (_, _) => { if (!finished) { finished = true; _hierarchyRename = null; slot.Content = previous; } };
            input.Focus(); input.SelectAll();
        }, DispatcherPriority.Input);
    }
}
