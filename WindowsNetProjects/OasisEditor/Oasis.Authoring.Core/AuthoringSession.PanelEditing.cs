namespace OasisEditor;

public sealed record PointSnapshot(double X, double Y);

public sealed record FaceSourceShapeSnapshot(string Id, string Name, string Type, IReadOnlyList<PointSnapshot> Points);

public sealed partial class AuthoringSession
{
    public IReadOnlyList<string> SelectedIds => _selectedIds.ToArray();

    public IReadOnlyList<FaceSourceShapeSnapshot> SourceShapes =>
        (_document?.FaceSourceShapes ?? []).Select(shape => new FaceSourceShapeSnapshot(
            shape.Id, shape.Name, shape.Type, shape.Points.Select(point => new PointSnapshot(point.X, point.Y)).ToArray())).ToArray();

    public void SelectElements(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var elements = RequireDocument().Elements;
        var known = elements.Select(element => element.ObjectId).ToHashSet(StringComparer.Ordinal);
        var selection = new List<string>(ids.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !known.Contains(id))
                throw new KeyNotFoundException($"Panel element '{id}' was not found.");
            if (seen.Add(id)) selection.Add(id);
        }
        if (_selectedIds.SequenceEqual(selection, StringComparer.Ordinal)) return;
        _selectedIds = selection;
        _selectedId = selection.FirstOrDefault();
        StorePanelState();
        Changed?.Invoke();
    }

    public bool MoveSelected(double dx, double dy)
    {
        ValidateGeometry(dx, dy, null, null);
        var document = RequireDocument();
        var indices = SelectedIndices(document, _selectedIds);
        if (dx == 0 && dy == 0 || indices.Length == 0) return false;
        foreach (var index in indices)
            if (document.Elements[index].LockTransform)
                throw new InvalidOperationException($"Element '{document.Elements[index].ObjectId}' has locked geometry.");

        var updated = (PanelElementFile[])document.Elements.Clone();
        foreach (var index in indices)
        {
            var element = updated[index];
            var x = element.X + dx;
            var y = element.Y + dy;
            ValidateGeometry(x, y, null, null);
            updated[index] = element with { X = x, Y = y };
        }
        Commit(document with { Elements = updated });
        return true;
    }

    public bool SetSelectedObjectState(bool? visible = null, bool? locked = null)
    {
        var document = RequireDocument();
        var indices = SelectedIndices(document, _selectedIds);
        var elements = (PanelElementFile[])document.Elements.Clone();
        foreach (var index in indices)
            elements[index] = elements[index] with { IsVisible = visible ?? elements[index].IsVisible, LockTransform = locked ?? elements[index].LockTransform };
        if (elements.SequenceEqual(document.Elements)) return false;
        Commit(document with { Elements = elements });
        return true;
    }

    public bool DeleteSelected()
    {
        var document = RequireDocument();
        var indices = SelectedIndices(document, _selectedIds);
        if (indices.Length == 0) return false;
        var removed = indices.Select(index => document.Elements[index].ObjectId).ToHashSet(StringComparer.Ordinal);
        Commit(document with { Elements = document.Elements.Where(element => !removed.Contains(element.ObjectId)).ToArray() }, notify: false);
        _selectedIds = [];
        _selectedId = null;
        StorePanelState();
        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<string> DuplicateSelected(double offsetX = 16, double offsetY = 16)
    {
        ValidateGeometry(offsetX, offsetY, null, null);
        var document = RequireDocument();
        var indices = SelectedIndices(document, _selectedIds);
        if (indices.Length == 0) return [];
        var copies = new List<PanelElementFile>(indices.Length);
        foreach (var index in indices)
        {
            var source = document.Elements[index];
            var x = source.X + offsetX;
            var y = source.Y + offsetY;
            ValidateGeometry(x, y, source.Width, source.Height);
            copies.Add(source with { ObjectId = Guid.NewGuid().ToString("N"), X = x, Y = y });
        }
        Commit(document with { Elements = [.. document.Elements, .. copies] }, notify: false);
        _selectedIds = copies.Select(copy => copy.ObjectId).ToList();
        _selectedId = _selectedIds.FirstOrDefault();
        StorePanelState();
        Changed?.Invoke();
        return _selectedIds.ToArray();
    }

    public bool MoveSelectionInOrder(bool forward)
    {
        var document = RequireDocument();
        var selected = _selectedIds.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0) return false;
        var elements = document.Elements.ToList();
        if (forward)
        {
            for (var i = elements.Count - 2; i >= 0; i--)
                if (selected.Contains(elements[i].ObjectId) && !selected.Contains(elements[i + 1].ObjectId))
                    (elements[i], elements[i + 1]) = (elements[i + 1], elements[i]);
        }
        else
        {
            for (var i = 1; i < elements.Count; i++)
                if (selected.Contains(elements[i].ObjectId) && !selected.Contains(elements[i - 1].ObjectId))
                    (elements[i], elements[i - 1]) = (elements[i - 1], elements[i]);
        }
        if (elements.SequenceEqual(document.Elements)) return false;
        Commit(document with { Elements = elements.ToArray() });
        return true;
    }

    public string AddElement(string kind, string name, double x, double y, double width, double height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateGeometry(x, y, width, height);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Element dimensions must be greater than zero.");
        var normalizedKind = kind.Trim().ToLowerInvariant();
        if (normalizedKind is not ("rectangle" or "anchor" or "zone" or "label"))
            throw new ArgumentException("Only rectangle, anchor, zone, and label elements can be added here.", nameof(kind));
        var id = Guid.NewGuid().ToString("N");
        var element = new PanelElementFile
        {
            ObjectId = id, Name = name.Trim(), Kind = normalizedKind, X = x, Y = y, Width = width, Height = height,
            IsVisible = true
        };
        var document = RequireDocument();
        Commit(document with { Elements = [.. document.Elements, element] }, notify: false);
        _selectedIds = [id];
        _selectedId = id;
        StorePanelState();
        Changed?.Invoke();
        return id;
    }

    public string AddSourceShape(string name, double? x = null, double? y = null, double? width = null, double? height = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateGeometry(x, y, width, height);
        var document = RequireDocument();
        double left, top, shapeWidth, shapeHeight;
        if (x.HasValue || y.HasValue || width.HasValue || height.HasValue)
        {
            var visible = document.Elements.Where(element => element.IsVisible != false).ToArray();
            var defaultBounds = visible.Length == 0
                ? (X: 0d, Y: 0d, Width: 640d, Height: 480d)
                : (X: visible.Min(element => element.X), Y: visible.Min(element => element.Y),
                    Width: visible.Max(element => element.X + element.Width) - visible.Min(element => element.X),
                    Height: visible.Max(element => element.Y + element.Height) - visible.Min(element => element.Y));
            left = x ?? defaultBounds.X; top = y ?? defaultBounds.Y;
            shapeWidth = width ?? defaultBounds.Width; shapeHeight = height ?? defaultBounds.Height;
        }
        else
        {
            var visible = document.Elements.Where(element => element.IsVisible != false).ToArray();
            if (visible.Length == 0) (left, top, shapeWidth, shapeHeight) = (0, 0, 640, 480);
            else
            {
                left = visible.Min(element => element.X);
                top = visible.Min(element => element.Y);
                shapeWidth = visible.Max(element => element.X + element.Width) - left;
                shapeHeight = visible.Max(element => element.Y + element.Height) - top;
            }
        }
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(shapeWidth) || !double.IsFinite(shapeHeight)
            || shapeWidth <= 0 || shapeHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Source shape bounds must have positive finite dimensions.");
        var id = $"source-shape-{Guid.NewGuid():N}";
        var shape = new PanelFaceSourceShapeFile
        {
            Id = id, Name = name.Trim(), Type = "perspectiveRectangle",
            Points = [new(left, top), new(left + shapeWidth, top), new(left + shapeWidth, top + shapeHeight), new(left, top + shapeHeight)]
        };
        Commit(document with { FaceSourceShapes = [.. (document.FaceSourceShapes ?? []), shape] });
        return id;
    }

    public bool UpdateSourceShape(string id, string name, PointSnapshot[] corners)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(corners);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Source shape name cannot be blank.", nameof(name));
        if (corners.Length != 4) throw new ArgumentException("A source shape must contain exactly four corners.", nameof(corners));
        ValidateConvexQuad(corners);
        var document = RequireDocument();
        var index = Array.FindIndex(document.FaceSourceShapes, shape => shape.Id == id);
        if (index < 0) throw new KeyNotFoundException($"Face source shape '{id}' was not found.");
        var current = document.FaceSourceShapes[index];
        var updatedShape = current with { Name = name.Trim(), Points = corners.Select(point => new PanelPointFile(point.X, point.Y)).ToArray() };
        if (updatedShape.Name == current.Name
            && updatedShape.Type == current.Type
            && updatedShape.Points.SequenceEqual(current.Points)) return false;
        var shapes = (PanelFaceSourceShapeFile[])document.FaceSourceShapes.Clone();
        shapes[index] = updatedShape;
        Commit(document with { FaceSourceShapes = shapes });
        return true;
    }

    public bool DeleteSourceShape(string id)
    {
        var document = RequireDocument();
        var index = Array.FindIndex(document.FaceSourceShapes, shape => shape.Id == id);
        if (index < 0) throw new KeyNotFoundException($"Face source shape '{id}' was not found.");
        Commit(document with { FaceSourceShapes = document.FaceSourceShapes.Where((_, shapeIndex) => shapeIndex != index).ToArray() });
        return true;
    }

    private static int[] SelectedIndices(Panel2DDocumentFile document, IReadOnlyList<string> ids)
    {
        var indices = new List<int>(ids.Count);
        foreach (var id in ids)
        {
            var index = Array.FindIndex(document.Elements, element => element.ObjectId == id);
            if (index < 0) throw new KeyNotFoundException($"Panel element '{id}' was not found.");
            indices.Add(index);
        }
        return indices.ToArray();
    }

    private static void ValidateConvexQuad(IReadOnlyList<PointSnapshot> points)
    {
        for (var index = 0; index < 4; index++)
        {
            if (points[index] is null || !double.IsFinite(points[index].X) || !double.IsFinite(points[index].Y))
                throw new ArgumentException("Source shape corners must contain finite coordinates.", nameof(points));
        }
        var sign = 0;
        for (var index = 0; index < 4; index++)
        {
            var a = points[index]; var b = points[(index + 1) % 4]; var c = points[(index + 2) % 4];
            var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (!double.IsFinite(cross) || Math.Abs(cross) < 1e-9)
                throw new ArgumentException("Source shape corners must form a nondegenerate convex quadrilateral.", nameof(points));
            var currentSign = Math.Sign(cross);
            if (sign != 0 && currentSign != sign)
                throw new ArgumentException("Source shape corners must form a convex quadrilateral in order.", nameof(points));
            sign = currentSign;
        }
    }
}
