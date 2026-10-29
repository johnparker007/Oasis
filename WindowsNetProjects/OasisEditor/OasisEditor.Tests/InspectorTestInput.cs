using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OasisEditor.Avalonia.Tests;

internal static class InspectorTestInput
{
    public static void Commit(TextBox field) => field.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
}
