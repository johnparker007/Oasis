using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public static class EditorKeyboardShortcuts
{
    private static readonly ReadOnlyCollection<EditorKeyboardShortcut> Shortcuts =
        new(
        [
            new EditorKeyboardShortcut(CanvasPanBehavior.UndoCommand, new KeyGesture(Key.Z, ModifierKeys.Control)),
            new EditorKeyboardShortcut(CanvasPanBehavior.RedoCommand, new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)),
        ]);

    public static string UndoGestureText => GetGestureText(CanvasPanBehavior.UndoCommand);

    public static string RedoGestureText => GetGestureText(CanvasPanBehavior.RedoCommand);

    public static void RegisterWindowBindings(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        foreach (var shortcut in Shortcuts)
        {
            window.InputBindings.Add(shortcut.ToKeyBinding());
        }
    }

    private static string GetGestureText(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return Shortcuts.FirstOrDefault(shortcut => ReferenceEquals(shortcut.Command, command))?.GestureText ?? string.Empty;
    }

    private sealed record EditorKeyboardShortcut(ICommand Command, KeyGesture Gesture)
    {
        public string GestureText => Gesture.GetDisplayStringForCulture(CultureInfo.CurrentUICulture);

        public KeyBinding ToKeyBinding() => new(Command, Gesture);
    }
}
