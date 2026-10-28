using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace OasisEditor.Avalonia;

/// <summary>
/// Hosts the workspace's stateful controls across Dock's deferred presenter lifetimes.
/// During a live move the destination can attach before the source presenter detaches.
/// Only the current attached presenter may parent a particular pane control.
/// </summary>
internal sealed class DockPanePresenter : ContentControl
{
    public static readonly StyledProperty<object?> PaneContextProperty =
        AvaloniaProperty.Register<DockPanePresenter, object?>(nameof(PaneContext));

    private sealed class Ownership { public DockPanePresenter? Presenter; }
    private static readonly ConditionalWeakTable<Control, Ownership> Owners = new();
    private Control? _owned;
    private bool _attached;
    internal bool IsTemporaryHost { get; init; }

    // A maximised view temporarily borrows this presenter's pane while its dock
    // remains attached. Reclaim the pane when that temporary view is removed.
    internal void RestorePresentation()
    {
        if (_attached) Present();
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);

    public object? PaneContext
    {
        get => GetValue(PaneContextProperty);
        set => SetValue(PaneContextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaneContextProperty && _attached) Present();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Present();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        Release();
        base.OnDetachedFromVisualTree(e);
    }

    private void Present()
    {
        if (PaneContext is not Control control)
        {
            Release();
            Content = PaneContext;
            return;
        }

        var ownership = Owners.GetOrCreateValue(control);
        if (ReferenceEquals(_owned, control) && ReferenceEquals(ownership.Presenter, this)) return;
        Release();
        // A deferred dock presenter may finish loading after maximise. It must
        // not take the pane back from the temporary host while it is in use.
        if (ownership.Presenter is { IsTemporaryHost: true } && !IsTemporaryHost) return;
        var previous = ownership.Presenter;
        ownership.Presenter = this;
        // Clear and measure-release the source before assigning the destination.
        // Clearing Content alone can leave a deferred ContentPresenter.Child parented.
        previous?.Release();
        _owned = control;
        Content = control;
    }

    private void Release()
    {
        if (_owned is { } control)
        {
            _owned = null;
            if (Owners.TryGetValue(control, out var ownership) && ReferenceEquals(ownership.Presenter, this))
                ownership.Presenter = null;
        }
        Content = null;
        Presenter?.UpdateChild();
    }
}
