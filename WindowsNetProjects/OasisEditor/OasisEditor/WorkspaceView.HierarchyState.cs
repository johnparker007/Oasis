using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private Button HierarchyStateButton(Control row, ElementSnapshot item, bool visibility)
    {
        var restricted = visibility ? !item.Visible : item.Locked;
        var inherited = visibility ? !item.AncestorsVisible : item.AncestorsLocked;
        var action = visibility ? restricted ? "Show" : "Hide" : restricted ? "Unlock" : "Lock";
        var state = visibility ? restricted ? "Hidden locally." : "Locally visible." : restricted ? "Locked locally." : "Locally unlocked.";
        var hint = inherited ? (visibility ? "Hidden by parent group. " : "Locked by parent group. ") + state + " Click changes this item's local setting only."
            : state;
        var button = new Button
        {
            Name = visibility ? "HierarchyVisibility" : "HierarchyLock", Focusable = false,
            Content = new EditorIcon {Kind = visibility ? EditorIconKind.Hidden : EditorIconKind.Lock}
        };
        button.Classes.Add("HierarchyState");
        if (!restricted && !inherited) button.Classes.Add("inactive");
        if (!restricted && inherited) button.Classes.Add("inherited");
        // Keep the same foreground as the row's component label, including selection.
        button.Bind(TemplatedControl.ForegroundProperty, row.GetObservable(TextElement.ForegroundProperty));
        ToolTip.SetTip(button, action + " " + item.Name + "\n" + hint);
        global::Avalonia.Automation.AutomationProperties.SetName(button, action + " " + item.Name);
        global::Avalonia.Automation.AutomationProperties.SetHelpText(button, hint);
        button.AddHandler(PointerPressedEvent, (_, args) => args.Handled = true, RoutingStrategies.Bubble);
        button.Click += (_, args) =>
        {
            args.Handled = true;
            Try(() =>
            {
                // These commands target the row directly and never change selection.
                if (_faceActive) _faces.UpdateElement(item.Id, visible: visibility ? !item.Visible : null, locked: visibility ? null : !item.Locked);
                else if (item.Kind == "group") _session.UpdateGroup(item.Id, visible: visibility ? !item.Visible : null, locked: visibility ? null : !item.Locked);
                else _session.UpdateElement(item.Id, visible: visibility ? !item.Visible : null, locked: visibility ? null : !item.Locked);
            });
        };
        return button;
    }
}
