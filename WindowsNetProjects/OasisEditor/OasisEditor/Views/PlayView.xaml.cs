using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OasisEditor.Views;

public partial class PlayView : UserControl
{
    private DocumentTabViewModel? _subscribedDocument;
    private bool _isPreProcessInputHooked;

    public PlayView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCanvasFromSelection();
        AttachPreProcessInputHandler();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachPreProcessInputHandler();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainWindowViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshCanvasFromSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SelectedDocument))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void RefreshCanvasFromSelection()
    {
        var selected = ViewModel?.SelectedDocument;
        UpdateSelectedDocumentSubscription(selected);

        if (selected is null || selected.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            PlayCanvas.Children.Clear();
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        PanelLayoutMapper.ApplyPersistedLayout(PlayCanvas, selected.PanelLayoutJson, selected.RuntimeState);
        ApplyClickableCursorHints(selected);
    }

    private async void OnPlayCanvasPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnPlayCanvasPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }

    private async void OnRootPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyDownAsync(eventArgs);
    }

    private async void OnRootPreviewKeyUp(object sender, KeyEventArgs eventArgs)
    {
        await TryRouteKeyUpAsync(eventArgs);
    }

    private void OnPlayCanvasPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayCanvasFocused();
    }

    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        EnsurePlayCanvasFocused();
    }

    private async void OnPlayCanvasPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || !TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        await ViewModel.TryHandlePlayViewPointerDownAsync(visualElementId, isFocused: true, CancellationToken.None);
    }

    private async void OnPlayCanvasPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || !TryResolveVisualElementId(eventArgs.OriginalSource as DependencyObject, out var visualElementId))
        {
            return;
        }

        await ViewModel.TryHandlePlayViewPointerUpAsync(visualElementId, isFocused: true, CancellationToken.None);
    }


    private void OnPlayCanvasMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseDown(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasMouseMove(object sender, MouseEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseMove(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseUp(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleMouseWheel(PlayCanvas, eventArgs);
    }

    private void OnPlayCanvasLostMouseCapture(object sender, MouseEventArgs eventArgs)
    {
        CanvasPanZoomBehavior.HandleLostMouseCapture(PlayCanvas);
    }

    private async void OnPlayCanvasLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View focus loss", CancellationToken.None);
    }

    private async void OnPlayCanvasUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ReleaseAllPlayViewInputsAsync("Play View close", CancellationToken.None);
    }

    private bool TryResolveVisualElementId(DependencyObject? source, out Guid visualElementId)
    {
        var selectable = CanvasSelectionBehavior.FindSelectableElement(source, PlayCanvas);
        if (selectable is not null && Guid.TryParse(selectable.Uid?.Trim(), out visualElementId))
        {
            return true;
        }

        visualElementId = Guid.Empty;
        return false;
    }

    private void UpdateSelectedDocumentSubscription(DocumentTabViewModel? selected)
    {
        if (ReferenceEquals(_subscribedDocument, selected))
        {
            return;
        }

        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged -= OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged -= OnSelectedDocumentPropertyChanged;
        }

        _subscribedDocument = selected;
        if (_subscribedDocument is not null)
        {
            _subscribedDocument.PanelVisualStateChanged += OnSelectedDocumentPanelVisualStateChanged;
            _subscribedDocument.PropertyChanged += OnSelectedDocumentPropertyChanged;
        }
    }

    private void OnSelectedDocumentPanelVisualStateChanged(PanelVisualStateChangedEvent visualStateChanged)
    {
        if (_subscribedDocument is null || visualStateChanged.DocumentId != _subscribedDocument.DocumentId)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            PanelLayoutMapper.ApplyVisualState(PlayCanvas, _subscribedDocument, visualStateChanged, _subscribedDocument.RuntimeState);
        });
    }

    private void OnSelectedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.PanelLayoutJson))
        {
            Dispatcher.Invoke(RefreshCanvasFromSelection);
        }
    }

    private void ApplyClickableCursorHints(DocumentTabViewModel selectedDocument)
    {
        var clickableObjectIds = new HashSet<Guid>(
            (ViewModel?.InputDefinitions ?? [])
                .Where(input => input.LinkedVisualElementId.HasValue)
                .Select(input => input.LinkedVisualElementId!.Value));

        foreach (var visual in PlayCanvas.Children.OfType<FrameworkElement>())
        {
            var uid = visual.Uid?.Trim();
            var isClickable = !string.IsNullOrWhiteSpace(uid)
                && Guid.TryParse(uid, out var visualId)
                && clickableObjectIds.Contains(visualId);
            visual.Cursor = isClickable
                ? Cursors.Hand
                : Cursors.Arrow;
        }
    }

    private void EnsurePlayCanvasFocused()
    {
        if (!PlayCanvas.IsKeyboardFocusWithin)
        {
            Keyboard.Focus(PlayCanvas);
        }
    }

    private async Task TryRouteKeyDownAsync(KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin)
        {
            return;
        }

        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        var handled = await ViewModel.TryHandlePlayViewKeyDownAsync(key.ToString(), isFocused: true, eventArgs.IsRepeat, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private async Task TryRouteKeyUpAsync(KeyEventArgs eventArgs)
    {
        if (eventArgs.Handled || ViewModel is null || !IsKeyboardFocusWithin)
        {
            return;
        }

        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        var handled = await ViewModel.TryHandlePlayViewKeyUpAsync(key.ToString(), isFocused: true, CancellationToken.None);
        if (handled)
        {
            eventArgs.Handled = true;
        }
    }

    private void AttachPreProcessInputHandler()
    {
        if (_isPreProcessInputHooked)
        {
            return;
        }

        InputManager.Current.PreProcessInput += OnPreProcessInput;
        _isPreProcessInputHooked = true;
    }

    private void DetachPreProcessInputHandler()
    {
        if (!_isPreProcessInputHooked)
        {
            return;
        }

        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        _isPreProcessInputHooked = false;
    }

    private void OnPreProcessInput(object? sender, PreProcessInputEventArgs eventArgs)
    {
        if (eventArgs.StagingItem.Input is not KeyEventArgs keyEventArgs)
        {
            return;
        }

        if (keyEventArgs.RoutedEvent == Keyboard.PreviewKeyDownEvent)
        {
            _ = TryRouteKeyDownAsync(keyEventArgs);
        }
        else if (keyEventArgs.RoutedEvent == Keyboard.PreviewKeyUpEvent)
        {
            _ = TryRouteKeyUpAsync(keyEventArgs);
        }
    }
}
