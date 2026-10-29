using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private readonly HashSet<Control> _inspectorBindings = [];
    private bool _inspectorCommitting;
    private long _sceneInspectorRevision;
    private object? _sceneInspectorKey;
    private object SceneInspectorKey() => (_sceneKind, _selectedSceneNodeId, _selectedReflectionId, _sceneInspectorRevision);
    private readonly Dictionary<TextBox, Func<bool>> _inspectorTextCommits = [];

    private void IgnoreInspectorActionFields() => _inspectorBindings.UnionWith(
        _inspector.GetLogicalDescendants().OfType<Control>().Where(c => c is TextBox or CheckBox or ComboBox));

    private bool CommitInspectorText(object? pointerSource = null)
    {
        var valid = true;
        foreach (var (box, commit) in _inspectorTextCommits.ToArray())
        {
            if (pointerSource is global::Avalonia.Visual visual &&
                (ReferenceEquals(visual, box) || visual.GetVisualAncestors().Contains(box))) continue;
            valid &= commit();
        }
        return valid;
    }

    private void ClearInspector()
    {
        _inspectorBindings.Clear();
        _inspectorTextCommits.Clear();
        _sceneInspectorKey = null;
        _inspector.Children.Clear();
        _fields.Clear();
    }

    // Each property section registers its controls after assigning initial values.
    // Text commits as one command on Enter/focus loss; discrete options commit immediately.
    private void BindInspectorEdits(Action apply, params Control[] controls)
    {
        if (controls.Length == 0) controls = _inspector.GetLogicalDescendants().OfType<Control>()
            .Where(c => c is TextBox or CheckBox or ComboBox).ToArray();
        var section = controls.Where(c => _inspectorBindings.Add(c)).ToArray();
        foreach (var control in section)
        {
            bool Commit()
            {
                // A removed Inspector must never write into the newly selected object.
                if (!_inspectorBindings.Contains(control) || !control.GetLogicalAncestors().Contains(_inspector)) return false;
                _inspectorCommitting = true;
                try
                {
                    apply();
                    foreach (var field in section) DataValidationErrors.ClearErrors(field);
                    if (Is3DActive) _sceneInspectorKey = SceneInspectorKey();
                    else if (!_machineActive) _inspected = InspectorKey();
                    return true;
                }
                catch (Exception error) when (error is not (OutOfMemoryException or OperationCanceledException))
                { DataValidationErrors.SetError(control, error); SetStatus(error.Message); return false; }
                finally { _inspectorCommitting = false; }
            }
            if (control is TextBox box)
            {
                var committed = box.Text;
                bool Save()
                {
                    if (box.Text == committed) return true;
                    if (!Commit()) return false;
                    committed = box.Text; return true;
                }
                _inspectorTextCommits[box] = Save;
                box.LostFocus += (_, _) => Save();
                box.KeyDown += (_, args) =>
                {
                    if (args.Key == Key.Enter)
                    {
                        args.Handled = true;
                        if (Save()) { _inspector.Focusable = true; _inspector.Focus(); }
                    }
                    else if (args.Key == Key.Escape)
                    { args.Handled = true; box.Text = committed; DataValidationErrors.ClearErrors(box); }
                };
            }
            else if (control is CheckBox check) check.IsCheckedChanged += (_, _) => Commit();
            else if (control is ComboBox combo) combo.SelectionChanged += (_, _) => Commit();
        }
    }

    private object InspectorKey()
    {
        var shape = !_faceActive ? _session.SourceShapes.FirstOrDefault(s => s.Id == _selectedSource) : null;
        var selected = ActiveElements.FirstOrDefault(e => SelectedIds.Contains(e.Id));
        return shape is not null ? (object)shape : SelectedIds.Count > 1 ? string.Join("|", SelectedIds)
            : _faceActive ? (_faces.Elements.FirstOrDefault(e => e.Id == selected?.Id), _faces.RenderRevision)
            : selected?.Kind == "group" ? (_session.Groups.First(g => g.Id == selected.Id), _session.RenderRevision)
            : (object?)selected ?? "none";
    }
}
