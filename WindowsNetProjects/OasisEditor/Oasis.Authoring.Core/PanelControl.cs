namespace OasisEditor;

/// <summary>A logical imported control owns its lamp layers and optional input.</summary>
public sealed record PanelControl
{
    public string Kind { get; init; } = "lamp";
    public string SourceFormat { get; init; } = "MFME";
    public int? SourceComponentIndex { get; init; }
    public double Width { get; init; } = 1;
    public double Height { get; init; } = 1;
    public PanelInputBinding? Input { get; init; }
}

/// <summary>Immutable, undoable input data; output lamp channels remain on their visual layers.</summary>
public sealed record PanelInputBinding
{
    public string Id { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Name { get; init; } = "";
    public InputDefinitionKind Kind { get; init; }
    public string ButtonNumber { get; init; } = "";
    public bool CoinInput { get; init; }
    public int? CoinChannel { get; init; }
    public int? CoinValue { get; init; }
    public bool Inverted { get; init; }
    public string RawMfmeShortcut { get; init; } = "";
    public string KeyboardShortcut { get; init; } = "";
    public string Notes { get; init; } = "";
    public static PanelInputBinding FromDefinition(InputDefinitionModel input) => new()
    {
        Id=input.Id,Name=input.Name,Kind=input.Kind,ButtonNumber=input.ButtonNumber,CoinInput=input.CoinInput,
        CoinChannel=input.CoinChannel,CoinValue=input.CoinValue,Inverted=input.Inverted,RawMfmeShortcut=input.RawMfmeShortcut,
        KeyboardShortcut=input.KeyboardShortcut,Notes=input.Notes
    };
    public InputDefinitionModel ToDefinition(string ownerId) => new()
    {
        Id=Id,Name=Name,Kind=Kind,ButtonNumber=ButtonNumber,CoinInput=CoinInput,CoinChannel=CoinChannel,CoinValue=CoinValue,
        Inverted=Inverted,RawMfmeShortcut=RawMfmeShortcut,KeyboardShortcut=KeyboardShortcut,Notes=Notes,
        LinkedVisualElementId=Guid.TryParse(ownerId,out var id)?id:null
    };
}

public sealed record PanelControlInstance(string Id,string Name,PanelControl Control,PanelTransform Transform,bool Visible,bool Locked);
public sealed record PanelGroupLabel(string Id,string Name,string DisplayName,string Type,int Count,string Icon,bool AutomaticName);

public sealed partial class AuthoringSession
{
    private Panel2DDocumentFile? _labelsVersion;
    private Dictionary<string,PanelGroupLabel> _groupLabels=[];
    private Dictionary<string,PanelGroupLabel> LabelMap
    {
        get
        {
            if(_document is null)return [];
            if(!ReferenceEquals(_labelsVersion,_document)){_groupLabels=Labels(_document);_labelsVersion=_document;}
            return _groupLabels;
        }
    }
    public IReadOnlyList<PanelGroupLabel> GroupLabels => LabelMap.Values.ToArray();
    public PanelGroupLabel GroupLabel(string id) => LabelMap.TryGetValue(id,out var label)?label:throw new KeyNotFoundException("Group not found.");
    private static Dictionary<string,PanelGroupLabel> Labels(Panel2DDocumentFile d)
    {
        var groups=d.Groups.ToDictionary(g=>g.Id);var leaves=d.Elements.ToDictionary(e=>e.ObjectId);
        var parents=d.Groups.ToDictionary(g=>g.Id,g=>g.ParentId);
        foreach(var element in d.Elements)parents.Add(element.ObjectId,element.ParentId);
        var children=NodeIds(d).ToLookup(id=>parents[id]??"");
        return d.Groups.ToDictionary(g=>g.Id,g=>
        {
            var kinds=new List<string>();var stack=new Stack<string>(children[g.Id]);
            while(stack.TryPop(out var id))
                if(groups.TryGetValue(id,out var child))
                {if(child.Control is {} control)kinds.Add(control.Kind);else foreach(var nested in children[id])stack.Push(nested);}
                else kinds.Add(leaves[id].Kind.ToLowerInvariant());
            var kind=kinds.Distinct(StringComparer.OrdinalIgnoreCase).Count()==1?kinds[0]:kinds.Count==0?"group":"mixed";
            if(g.Control is {} own)kind=own.Kind;
            var type=kind switch {"lamp"=>"Lamp","reel"=>"Reel","sevensegment"=>"7-segment display","alpha"=>"Alpha display","background"=>"Background","image"=>"Image","label"=>"Label","rectangle"=>"Rectangle","vfddotmatrix"=>"VFD display","mixed"=>"Mixed",_=>"Group"};
            var name=g.AutomaticName?type:g.Name;
            var icon=g.AutomaticIcon?kind switch {"lamp"=>"Lamp","reel"=>"Reel","sevensegment"=>"SevenSegment",_=>"Hierarchy"}:g.Icon;
            var count=g.Control is null?kinds.Count:1;
            return new PanelGroupLabel(g.Id,name,g.Control is null?$"{name} ({count})":name,type,count,icon,g.AutomaticName);
        });
    }
    public IReadOnlyList<ElementSnapshot> ControlChannels(string id) => Elements.Where(e=>e.ParentId==id).ToArray();
    public void UpdateControlInput(string id,PanelInputBinding? input)
    {
        var d=RequireDocument();var g=d.Groups.Single(g=>g.Id==id);
        if(g.Control is null)throw new ArgumentException("Select an imported control.");
        var next=input??(g.Control.Input is {} previous?previous with {Enabled=false}:null);
        Commit(d with {Groups=d.Groups.Select(v=>v.Id==id?v with {Control=g.Control with {Input=next}}:v).ToArray()});
    }
}
