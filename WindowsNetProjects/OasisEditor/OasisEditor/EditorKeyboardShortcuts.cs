using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OasisEditor;

public static class EditorKeyboardShortcuts
{
    private static readonly DependencyProperty IsRegisteredProperty =
        DependencyProperty.RegisterAttached(
            "IsRegistered",
            typeof(bool),
            typeof(EditorKeyboardShortcuts),
            new PropertyMetadata(false));

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

        if ((bool)window.GetValue(IsRegisteredProperty))
        {
            return;
        }

        window.SetValue(IsRegisteredProperty, true);
        window.PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private static string GetGestureText(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return Shortcuts.FirstOrDefault(shortcut => ReferenceEquals(shortcut.Command, command))?.GestureText ?? string.Empty;
    }

    private sealed record EditorKeyboardShortcut(ICommand Command, KeyGesture Gesture)
    {
        public string GestureText => Gesture.GetDisplayStringForCulture(CultureInfo.CurrentUICulture);
    }

    private static void OnWindowPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (sender is not Window window || eventArgs.Handled)
        {
            return;
        }

        var shortcut = Shortcuts.FirstOrDefault(binding => binding.Gesture.Matches(window, eventArgs));
        if (shortcut is null)
        {
            return;
        }

        var target = Keyboard.FocusedElement as IInputElement ?? window;
        if (TryExecuteShortcut(shortcut, target))
        {
            eventArgs.Handled = true;
            return;
        }

        if (!ReferenceEquals(target, window) && TryExecuteShortcut(shortcut, window))
        {
            eventArgs.Handled = true;
        }
    }

    private static bool TryExecuteShortcut(EditorKeyboardShortcut shortcut, IInputElement target)
    {
        if (shortcut.Command is RoutedCommand routedCommand)
        {
            if (!routedCommand.CanExecute(null, target))
            {
                return false;
            }

            routedCommand.Execute(null, target);
            return true;
        }

        if (!shortcut.Command.CanExecute(null))
        {
            return false;
        }

        shortcut.Command.Execute(null);
        return true;
    }
}
