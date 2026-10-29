namespace OasisEditor;

public sealed partial class AuthoringSession
{
    public IReadOnlyList<PanelGroup> Groups => _document?.Groups ?? [];
    private static string[] NodeIds(Panel2DDocumentFile d) => d.Groups.Select(g => g.Id).Concat(d.Elements.Select(e => e.ObjectId)).ToArray();
    private static string? Parent(Panel2DDocumentFile d, string id) => d.Groups.FirstOrDefault(g => g.Id == id)?.ParentId ?? d.Elements.FirstOrDefault(e => e.ObjectId == id)?.ParentId;
    private static string[] OrderedIds(Panel2DDocumentFile d) => d.HierarchyOrder.Concat(NodeIds(d)).Distinct(StringComparer.Ordinal).Where(NodeIds(d).Contains).ToArray();
    private static HashSet<string> Descendants(Panel2DDocumentFile d, IEnumerable<string> ids)
    {
        var result = ids.ToHashSet(StringComparer.Ordinal); var pending = new Queue<string>(result);
        var children = NodeIds(d).ToLookup(id => Parent(d,id) ?? "");
        while (pending.TryDequeue(out var id)) foreach (var child in children[id]) if (result.Add(child)) pending.Enqueue(child);
        return result;
    }
    private static string[] SelectionRoots(Panel2DDocumentFile d, IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        return OrderedIds(d).Where(selected.Contains).Where(id =>
        { for(var p=Parent(d,id); p is not null; p=Parent(d,p)) if(selected.Contains(p)) return false; return true; }).ToArray();
    }
    internal static Panel2DDocumentFile NormalizeHierarchy(Panel2DDocumentFile d)
    {
        ValidateGroups(d);
        if (d.Groups.Length == 0 && d.HierarchyOrder.Length == 0) return d;
        var ordered = OrderedIds(d); var children = ordered.ToLookup(id => Parent(d,id) ?? "");
        var result = new List<string>(); var stack = new Stack<string>(children[""].Reverse());
        while(stack.TryPop(out var id)) { result.Add(id); foreach(var child in children[id].Reverse()) stack.Push(child); }
        var elements = d.Elements.ToDictionary(e=>e.ObjectId, StringComparer.Ordinal);
        return d with { HierarchyOrder = result.ToArray(), Elements = result.Where(elements.ContainsKey).Select(id=>elements[id]).ToArray() };
    }
    internal static void ValidateGroups(Panel2DDocumentFile d)
    {
        if(d.Groups is null || d.HierarchyOrder is null) throw new ArgumentException("Panel hierarchy collections cannot be null.");
        var ids = new HashSet<string>(d.Elements.Select(e=>e.ObjectId), StringComparer.Ordinal);
        foreach(var group in d.Groups)
            if(group is null || string.IsNullOrWhiteSpace(group.Id) || !ids.Add(group.Id) || string.IsNullOrWhiteSpace(group.Name)
                || !PanelHierarchy.Icons.Contains(group.Icon) || group.Transform is null)
                throw new ArgumentException("Groups require unique IDs, names, valid transforms and a built-in icon.");
        var tree = new PanelHierarchy(d.Groups);
        foreach(var e in d.Elements) { tree.Resolve(e.ParentId); e.Transform?.Validate(); }
        if(d.HierarchyOrder.Any(id => !ids.Contains(id)) || d.HierarchyOrder.Distinct().Count()!=d.HierarchyOrder.Length)
            throw new ArgumentException("Hierarchy order contains duplicate or missing objects.");
    }
    private PanelHierarchy Tree => new(Groups);
    // Bake the transformed origin into the local inspector position, retaining
    // the full linear transform around that origin (including reparenting shear).
    private static PanelElementFile WithLocalTransform(PanelElementFile e,PanelTransform t,string? parentId)
    {
        var origin=t.Map(e.X,e.Y);
        var around=t with {X=origin.X-t.A*origin.X-t.C*origin.Y,Y=origin.Y-t.B*origin.X-t.D*origin.Y};
        return e with {X=origin.X,Y=origin.Y,ParentId=parentId,Transform=around.NearlyEquals(PanelTransform.Identity)?null:around};
    }
    private static PanelElementFile TranslateLocal(PanelElementFile e,double dx,double dy)
    {
        var t=e.Transform??PanelTransform.Identity;
        return e with {X=e.X+dx,Y=e.Y+dy,Transform=e.Transform is null?null:t with {X=t.X+dx-t.A*dx-t.C*dy,Y=t.Y+dy-t.B*dx-t.D*dy}};
    }
    public IReadOnlyList<ElementSnapshot> HierarchyElements
    {
        get
        {
            if(_document is not {} d) return [];
            var groups=d.Groups.ToDictionary(g=>g.Id); var elements=Elements.ToDictionary(e=>e.Id);
            return OrderedIds(NormalizeHierarchy(d)).Select(id=>
            {
                var depth=0; for(var p=Parent(d,id);p is not null;p=Parent(d,p)) depth++;
                return groups.TryGetValue(id,out var g) ? new ElementSnapshot(g.Id,"group",g.Name,g.Transform.X,g.Transform.Y,0,0,null,null,null,null,null,null,null,g.Visible,g.Locked,g.ParentId,g.Icon,depth)
                    : elements[id] with { Depth=depth };
            }).ToArray();
        }
    }
    private Panel2DDocumentFile? _viewportVersion;
    private IReadOnlyList<ElementSnapshot> _viewportElements=[];
    public IReadOnlyList<ElementSnapshot> ViewportElements
    {
        get
        {
            if(RenderDocument is not {} model) return [];
            if(ReferenceEquals(_viewportVersion,_document))return _viewportElements;
            var resolved=PanelHierarchy.ResolveDocument(model);
            var result=resolved.Elements.Select(e=>
            {
                var b=PanelHierarchy.Bounds(e); return Project(Panel2DDocumentStorage.ToStorageElement(e)) with {X=b.X,Y=b.Y,Width=b.Width,Height=b.Height};
            }).ToList();
            foreach(var g in Groups)
            {
                var ids=Descendants(RequireDocument(),[g.Id]);var children=result.Where(e=>ids.Contains(e.Id)&&e.Kind!="group").ToArray();
                var state=Tree.Resolve(g.Id);var origin=state.Transform.Map(0,0);
                var x=children.Length==0?origin.X:children.Min(e=>e.X);var y=children.Length==0?origin.Y:children.Min(e=>e.Y);
                result.Add(new(g.Id,"group",g.Name,x,y,children.Length==0?1:children.Max(e=>e.X+e.Width)-x,children.Length==0?1:children.Max(e=>e.Y+e.Height)-y,null,null,null,null,null,null,null,state.Visible,state.Locked,g.ParentId,g.Icon));
            }
            _viewportVersion=_document;return _viewportElements=result;
        }
    }
    public string? HitTestPanel(double x,double y)
    {
        if(RenderDocument is not {} model) return null;
        foreach(var e in PanelHierarchy.ResolveDocument(model).Elements.Reverse())
        {
            if(!e.IsVisible) continue;
            var p=(e.Transform??PanelTransform.Identity).Inverse().Map(x,y);
            if(p.X>=e.X && p.X<=e.X+e.Width && p.Y>=e.Y && p.Y<=e.Y+e.Height) return e.ObjectId;
        }
        return null;
    }
    public bool SelectionContainsDescendant(string id) => _document is {} d && Descendants(d,_selectedIds).Contains(id);
    public void ResizeElementInWorld(string id,double dx,double dy)
    {
        var e=RequireDocument().Elements.Single(e=>e.ObjectId==id);
        var inverse=(e.Transform??PanelTransform.Identity).Then(Tree.Resolve(e.ParentId).Transform).Inverse();
        UpdateElement(id,width:Math.Max(1,e.Width+inverse.A*dx+inverse.C*dy),height:Math.Max(1,e.Height+inverse.B*dx+inverse.D*dy));
    }
    public string CreateGroup(string name="Group",string? parentId=null,bool groupSelection=true,string icon="Hierarchy")
    {
        var d=RequireDocument();var selection=groupSelection?SelectionRoots(d,_selectedIds):[];
        if(selection.Length>0 && parentId is null && selection.Select(id=>Parent(d,id)).Distinct().Count()==1) parentId=Parent(d,selection[0]);
        var parent=Tree.Resolve(parentId); if(parent.Locked) throw new InvalidOperationException("The parent group is locked.");
        var id=Guid.NewGuid().ToString("N"); var group=new PanelGroup {Id=id,Name=name.Trim(),ParentId=parentId,Icon=icon};
        var order=OrderedIds(d).ToList(); var index=selection.Length==0?order.Count:order.IndexOf(selection[0]);order.Insert(index,id);
        d=d with {Groups=[..d.Groups,group],HierarchyOrder=order.ToArray()};
        if(selection.Length>0) d=ReparentCore(d,selection,id,append:false);
        Commit(d,notify:false);_selectedIds=[id];_selectedId=id;StorePanelState();Changed?.Invoke();return id;
    }
    public bool UpdateGroup(string id,string? name=null,double? x=null,double? y=null,double? rotation=null,double? scaleX=null,double? scaleY=null,string? icon=null,bool? visible=null,bool? locked=null)
    {
        var d=RequireDocument();var g=d.Groups.FirstOrDefault(g=>g.Id==id)??throw new KeyNotFoundException("Group not found.");
        var parts=g.Transform.Decompose();var t=x.HasValue||y.HasValue||rotation.HasValue||scaleX.HasValue||scaleY.HasValue
            ? PanelTransform.Compose(x??g.Transform.X,y??g.Transform.Y,rotation??parts.Rotation,scaleX??parts.ScaleX,scaleY??parts.ScaleY,parts.Shear):g.Transform;
        if(t.NearlyEquals(g.Transform))t=g.Transform;
        if(t!=g.Transform && Tree.Resolve(id).Locked) throw new InvalidOperationException("The group has locked geometry.");
        var next=g with {Name=name?.Trim()??g.Name,Transform=t,Icon=icon??g.Icon,Visible=visible??g.Visible,Locked=locked??g.Locked};
        if(next==g)return false;Commit(d with {Groups=d.Groups.Select(v=>v.Id==id?next:v).ToArray()});return true;
    }
    private static Panel2DDocumentFile ReparentCore(Panel2DDocumentFile d,string[] ids,string? parentId,bool append=true)
    {
        var roots=SelectionRoots(d,ids);var descendants=Descendants(d,roots);var tree=new PanelHierarchy(d.Groups);
        if(parentId is not null && descendants.Contains(parentId))throw new ArgumentException("A group cannot be moved into its own descendants.");
        var target=tree.Resolve(parentId);if(target.Locked)throw new InvalidOperationException("The target group is locked.");
        var inverse=target.Transform.Inverse();var selected=roots.ToHashSet();
        foreach(var id in roots)if(tree.Resolve(Parent(d,id)).Locked || d.Groups.Any(g=>g.Id==id&&g.Locked) || d.Elements.Any(e=>e.ObjectId==id&&e.LockTransform))throw new InvalidOperationException("The selection has locked geometry.");
        var groups=d.Groups.Select(g=>selected.Contains(g.Id)?g with {ParentId=parentId,Transform=tree.Resolve(g.Id).Transform.Then(inverse)}:g).ToArray();
        var elements=d.Elements.Select(e=>selected.Contains(e.ObjectId)?WithLocalTransform(e,(e.Transform??PanelTransform.Identity).Then(tree.Resolve(e.ParentId).Transform).Then(inverse),parentId):e).ToArray();
        var order=OrderedIds(d); if(append)order=order.Where(id=>!descendants.Contains(id)).Concat(order.Where(descendants.Contains)).ToArray();
        return d with {Groups=groups,Elements=elements,HierarchyOrder=order};
    }
    public void ReparentSelection(string? parentId) { var d=RequireDocument();Commit(ReparentCore(d,_selectedIds.ToArray(),parentId)); }
    public void ReorderSelection(string targetId,bool after=false)
    {
        var d=RequireDocument();var roots=SelectionRoots(d,_selectedIds);var moving=Descendants(d,roots);
        if(!NodeIds(d).Contains(targetId))throw new KeyNotFoundException("Target object not found.");
        if(moving.Contains(targetId))return;
        var order=OrderedIds(d);var block=order.Where(moving.Contains).ToArray();var rest=order.Where(id=>!moving.Contains(id)).ToList();
        var index=rest.IndexOf(targetId);
        if(after){var targetTree=Descendants(d,[targetId]);while(index+1<rest.Count&&targetTree.Contains(rest[index+1]))index++;index++;}
        rest.InsertRange(index,block);
        d=ReparentCore(d,roots,Parent(d,targetId),append:false);
        Commit(d with {HierarchyOrder=rest.ToArray()});
    }
    public void UngroupSelection()
    {
        var d=RequireDocument();var selected=SelectionRoots(d,_selectedIds);var children=new List<string>();
        foreach(var id in selected)
        {
            var g=d.Groups.FirstOrDefault(g=>g.Id==id);if(g is null)continue;
            var direct=OrderedIds(d).Where(child=>Parent(d,child)==id).ToArray();
            // Dissolving a group retains its inherited visibility/lock state.
            d=ReparentCore(d,direct,g.ParentId,append:false);
            d=d with {Groups=d.Groups.Where(v=>v.Id!=id).Select(v=>direct.Contains(v.Id)?v with {Visible=v.Visible&&g.Visible,Locked=v.Locked||g.Locked}:v).ToArray(),
                Elements=d.Elements.Select(e=>direct.Contains(e.ObjectId)?e with {IsVisible=(e.IsVisible??true)&&g.Visible,LockTransform=e.LockTransform||g.Locked}:e).ToArray(),
                HierarchyOrder=d.HierarchyOrder.Where(v=>v!=id).ToArray()};children.AddRange(direct);
        }
        Commit(d,notify:false);_selectedIds=children;_selectedId=children.FirstOrDefault();StorePanelState();Changed?.Invoke();
    }
    private bool MoveHierarchySelection(bool forward,bool toEnd=false)
    {
        var d=RequireDocument();var roots=SelectionRoots(d,_selectedIds).ToHashSet();var order=OrderedIds(d).ToList();var changed=false;
        foreach(var parent in roots.Select(id=>Parent(d,id)).Distinct())
        {
            var siblings=order.Where(id=>Parent(d,id)==parent).ToList();var before=siblings.ToArray();
            if(toEnd) siblings=forward?siblings.Where(id=>!roots.Contains(id)).Concat(siblings.Where(roots.Contains)).ToList():siblings.Where(roots.Contains).Concat(siblings.Where(id=>!roots.Contains(id))).ToList();
            else if(forward){for(var i=siblings.Count-2;i>=0;i--)if(roots.Contains(siblings[i])&&!roots.Contains(siblings[i+1]))(siblings[i],siblings[i+1])=(siblings[i+1],siblings[i]);}
            else {for(var i=1;i<siblings.Count;i++)if(roots.Contains(siblings[i])&&!roots.Contains(siblings[i-1]))(siblings[i],siblings[i-1])=(siblings[i-1],siblings[i]);}
            if(before.SequenceEqual(siblings))continue;changed=true;var index=0;for(var i=0;i<order.Count;i++)if(Parent(d,order[i])==parent)order[i]=siblings[index++];
        }
        if(changed)Commit(d with {HierarchyOrder=order.ToArray()});return changed;
    }
}
