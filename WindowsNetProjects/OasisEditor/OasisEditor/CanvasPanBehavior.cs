using System.Windows.Input;

namespace OasisEditor;

/// <summary>
/// Legacy WPF canvas behavior retired after Skia edit-view migration.
/// Keeps shared Undo/Redo routed commands for existing command bindings.
/// </summary>
public static class CanvasPanBehavior
{
    public static readonly RoutedUICommand UndoCommand = new("Undo", "Undo", typeof(CanvasPanBehavior));
    public static readonly RoutedUICommand RedoCommand = new("Redo", "Redo", typeof(CanvasPanBehavior));
}
