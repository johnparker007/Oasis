namespace OasisEditor;

/// <summary>Double precision affine transform; composition applies the right operand first.</summary>
public sealed record PanelTransform(double A = 1, double B = 0, double C = 0, double D = 1, double X = 0, double Y = 0)
{
    public static PanelTransform Identity { get; } = new();
    public (double X, double Y) Map(double x, double y) => (A*x + C*y + X, B*x + D*y + Y);
    public PanelTransform Then(PanelTransform parent) => new(parent.A*A + parent.C*B, parent.B*A + parent.D*B,
        parent.A*C + parent.C*D, parent.B*C + parent.D*D, parent.A*X + parent.C*Y + parent.X, parent.B*X + parent.D*Y + parent.Y);
    public PanelTransform Inverse()
    {
        var det = A*D - B*C;
        if (!double.IsFinite(det) || Math.Abs(det) < 1e-12) throw new ArgumentException("Group transforms must be invertible.");
        return new(D/det, -B/det, -C/det, A/det, (C*Y-D*X)/det, (B*X-A*Y)/det);
    }
    public void Validate()
    {
        if (new[] {A,B,C,D,X,Y}.Any(v => !double.IsFinite(v))) throw new ArgumentException("Transform values must be finite.");
        _ = Inverse();
    }
    public bool NearlyEquals(PanelTransform other) => new[] { (A,other.A),(B,other.B),(C,other.C),(D,other.D),(X,other.X),(Y,other.Y) }
        .All(v=>Math.Abs(v.Item1-v.Item2)<=1e-10*Math.Max(1,Math.Max(Math.Abs(v.Item1),Math.Abs(v.Item2))));
    public (double Rotation, double ScaleX, double ScaleY, double Shear) Decompose()
    {
        var sx = Math.Sqrt(A*A + B*B); var sy = (A*D-B*C)/sx;
        return (Math.Atan2(B,A)*180/Math.PI, sx, sy, (A*C+B*D)/(sx*sy));
    }
    public static PanelTransform Compose(double x, double y, double rotation, double sx, double sy, double shear = 0)
    {
        var r = rotation*Math.PI/180; var c = Math.Cos(r); var s = Math.Sin(r);
        var result = new PanelTransform(c*sx, s*sx, sy*(c*shear-s), sy*(s*shear+c), x,y); result.Validate(); return result;
    }
}

public sealed record PanelGroup
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "Group";
    public string? ParentId { get; init; }
    public PanelTransform Transform { get; init; } = new();
    public bool Visible { get; init; } = true;
    public bool Locked { get; init; }
    public string Icon { get; init; } = "Hierarchy";
    public bool AutomaticName { get; init; }
    public bool AutomaticIcon { get; init; }
    public PanelControl? Control { get; init; }
}

/// <summary>Resolves inherited transforms and state; the storage list follows hierarchy drawing order.</summary>
public sealed class PanelHierarchy
{
    public static IReadOnlyList<string> Icons { get; } = Array.AsReadOnly(new[] { "Hierarchy", "Inspector", "Project", "Console", "Lamp", "SevenSegment", "Reel", "Button" });
    private readonly Dictionary<string, PanelGroup> groups;
    private readonly Dictionary<string, (PanelTransform Transform, bool Visible, bool Locked)> resolved = new(StringComparer.Ordinal);
    public PanelHierarchy(IEnumerable<PanelGroup> source) { groups = source.ToDictionary(g => g.Id, StringComparer.Ordinal); foreach (var id in groups.Keys) Resolve(id); }
    public (PanelTransform Transform, bool Visible, bool Locked) Resolve(string? id)
    {
        if (id is null) return (PanelTransform.Identity, true, false);
        if (resolved.TryGetValue(id, out var cached)) return cached;
        var chain = new List<PanelGroup>(); var visited = new HashSet<string>(StringComparer.Ordinal); var current = id;
        while (current is not null && !resolved.ContainsKey(current))
        {
            if (!visited.Add(current)) throw new ArgumentException("A group cannot contain itself or one of its parents.");
            if (!groups.TryGetValue(current, out var group)) throw new ArgumentException("The parent group does not exist.");
            group.Transform.Validate(); chain.Add(group); current = group.ParentId;
        }
        var state = current is null ? (PanelTransform.Identity, true, false) : resolved[current];
        foreach (var group in chain.AsEnumerable().Reverse())
        { state = (group.Transform.Then(state.Item1), group.Visible && state.Item2, group.Locked || state.Item3); state.Item1.Validate(); resolved[group.Id] = state; }
        return resolved[id];
    }
    public PanelTransform World(PanelElementModel e) => (e.Transform ?? PanelTransform.Identity).Then(Resolve(e.ParentId).Transform);
    public static Panel2DDocumentModel ResolveDocument(Panel2DDocumentModel document)
    {
        if(document.Groups.Count==0 && document.Elements.All(e=>e.ParentId is null))return document;
        var tree = new PanelHierarchy(document.Groups);
        return new() { Title = document.Title, Summary = document.Summary, FaceSourceShapes = document.FaceSourceShapes,
            Controls = document.Controls.Concat(document.Groups.Where(g=>g.Control is not null).Select(g=>{var state=tree.Resolve(g.Id);return new PanelControlInstance(g.Id,g.AutomaticName?"Lamp":g.Name,g.Control!,state.Transform,state.Visible,state.Locked);})).ToArray(),
            Elements = document.Elements.Select(e => PanelElementModelCloner.Clone(e,
                isVisible: e.IsVisible && tree.Resolve(e.ParentId).Visible,
                isTransformLocked: e.IsTransformLocked || tree.Resolve(e.ParentId).Locked,
                transform: tree.World(e), overrideParent: true)).ToArray() };
    }
    public static (double X,double Y,double Width,double Height) Bounds(PanelElementModel e)
    {
        var t = e.Transform ?? PanelTransform.Identity;
        var corners = new[] {t.Map(e.X,e.Y),t.Map(e.X+e.Width,e.Y),t.Map(e.X+e.Width,e.Y+e.Height),t.Map(e.X,e.Y+e.Height)};
        var x=corners.Min(p=>p.X);var y=corners.Min(p=>p.Y);
        return(x,y,corners.Max(p=>p.X)-x,corners.Max(p=>p.Y)-y);
    }
}
