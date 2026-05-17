using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;

namespace OasisEditor.Views;

public partial class DocumentEditorView : UserControl
{
    public DocumentEditorView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel
        ?? Window.GetWindow(this)?.DataContext as MainWindowViewModel;

    private async void OnPanelCanvasPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var handled = await vm.TryHandlePlayViewKeyDownAsync(eventArgs.Key.ToString(), isFocused: true, eventArgs.IsRepeat, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private async void OnPanelCanvasPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var handled = await vm.TryHandlePlayViewKeyUpAsync(eventArgs.Key.ToString(), isFocused: true, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private async void OnPanelCanvasPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        await vm.TryHandlePlayViewPointerDownAsync(visualElementId, isFocused: true, CancellationToken.None);
    }

    private async void OnPanelCanvasPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        await vm.TryHandlePlayViewPointerUpAsync(visualElementId, isFocused: true, CancellationToken.None);
    }

    private async void OnPanelCanvasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View focus loss", CancellationToken.None);
    }

    private async void OnPanelCanvasUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View close", CancellationToken.None);
    }

    private bool TryResolveVisualElementId(DependencyObject? source, out Guid visualElementId)
    {
        var selectable = CanvasSelectionBehavior.FindSelectableElement(source, PanelCanvas);
        if (selectable is not null && Guid.TryParse(selectable.Uid?.Trim(), out visualElementId))
        {
            return true;
        }

        visualElementId = Guid.Empty;
        return false;
    }
}
