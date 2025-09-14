using UnityEngine;
using DynamicPanels;

// Handles maximising and restoring of a PanelTab's panel.
public sealed class PanelTabMaximiser : MonoBehaviour
{
    bool _maximised;
    byte[] _savedBytes;
    PanelHeader _panelHeader;
    PanelResizeHelper[] _resizeHelpers;
    PanelTab[] _tabs;
    Vector2 _lastCanvasSize;
    Panel _panel;

    public bool IsMaximised => _maximised;

    public void ToggleMaximise()
    {
        if (_maximised)
        {
            Restore();
        }
        else
        {
            Maximise();
        }
    }

    public void Maximise()
    {
        if (_maximised)
        {
            return;
        }

        var tab = GetComponent<PanelTab>();
        _panel = tab ? tab.Panel : null;
        if (_panel == null)
        {
            return;
        }

        _savedBytes = PanelSerialization.SerializeCanvasToArray(_panel.Canvas);
        _panel.Detach();
        _panel.BringForward();
        _panel.RectTransform.anchoredPosition = Vector2.zero;
        _lastCanvasSize = _panel.Canvas.Size;
        _panel.FloatingSize = _lastCanvasSize;

        _panelHeader = _panel.GetComponentInChildren<PanelHeader>();
        if (_panelHeader)
        {
            _panelHeader.enabled = false;
        }

        _resizeHelpers = _panel.GetComponentsInChildren<PanelResizeHelper>();
        foreach (var helper in _resizeHelpers)
        {
            if (helper)
            {
                helper.enabled = false;
            }
        }

        _tabs = _panel.GetComponentsInChildren<PanelTab>();
        foreach (var t in _tabs)
        {
            if (t)
            {
                t.enabled = false;
            }
        }

        _maximised = true;
    }

    public void Restore()
    {
        if (!_maximised)
        {
            return;
        }

        var tab = GetComponent<PanelTab>();
        _panel = tab ? tab.Panel : null;
        if (_panel == null)
        {
            _maximised = false;
            return;
        }

        if (_panelHeader)
        {
            _panelHeader.enabled = true;
        }

        if (_resizeHelpers != null)
        {
            foreach (var helper in _resizeHelpers)
            {
                if (helper)
                {
                    helper.enabled = true;
                }
            }
        }

        if (_tabs != null)
        {
            foreach (var t in _tabs)
            {
                if (t)
                {
                    t.enabled = true;
                }
            }
        }

        PanelSerialization.DeserializeCanvasFromArray(_panel.Canvas, _savedBytes);
        _maximised = false;
    }

    void Update()
    {
        if (_maximised && _panel != null)
        {
            var size = _panel.Canvas.Size;
            if (size != _lastCanvasSize)
            {
                _lastCanvasSize = size;
                _panel.FloatingSize = size;
                _panel.RectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}
