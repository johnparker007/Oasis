namespace OasisEditor;

public sealed partial class AuthoringSession
{
    /// <summary>Preserves mapper/painter order while keeping each MFME source control intact.</summary>
    internal static Panel2DDocumentFile AttachFmlControls(Panel2DDocumentFile document,
        IReadOnlyList<PanelElementModel> source,IReadOnlyList<InputDefinitionModel> inputs)
    {
        var groups=new List<PanelGroup>();var elements=new List<PanelElementFile>();var order=OrderedIds(document).ToList();
        PanelGroup? section=null;string? previousKind=null;
        for(var index=0;index<source.Count;)
        {
            var original=source[index];var kind=Panel2DDocumentStorage.SerializeElementKind(original.Kind);
            if(kind!=previousKind)
            {
                section=new PanelGroup {Id=Guid.NewGuid().ToString("N"),AutomaticName=true,AutomaticIcon=true};
                groups.Add(section);order.Add(section.Id);previousKind=kind;
            }
            if(original.Kind!=PanelElementKind.Lamp)
            {
                var element=Panel2DDocumentStorage.ToStorageElement(PanelElementModelCloner.Clone(original,objectId:Guid.NewGuid().ToString("N"),parentId:section!.Id,overrideParent:true));
                elements.Add(element);order.Add(element.ObjectId);index++;continue;
            }
            var end=index+1;
            while(end<source.Count && source[end].Kind==PanelElementKind.Lamp
                && original.SourceComponentIndex.HasValue && source[end].SourceComponentIndex==original.SourceComponentIndex
                && source[end].SharedSourceSetId==original.SharedSourceSetId)end++;
            var parts=source.Skip(index).Take(end-index).ToArray();
            var visualIds=parts.Select(p=>Guid.TryParse(p.ObjectId,out var id)?id:Guid.Empty).ToHashSet();
            var input=inputs.SingleOrDefault(i=>i.LinkedVisualElementId is {} visual&&visualIds.Contains(visual));
            var controlId=Guid.NewGuid().ToString("N");
            var name=!string.IsNullOrWhiteSpace(original.DisplayText)?original.DisplayText.Trim():input?.Name;
            name=string.IsNullOrWhiteSpace(name)?original.Name:name;
            name=string.Join(" ",(name??"Lamp").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
            var control=new PanelGroup {Id=controlId,Name=string.IsNullOrWhiteSpace(name)?"Lamp":name,ParentId=section!.Id,Icon="Lamp",
                Transform=new PanelTransform(X:original.X,Y:original.Y),
                Control=new PanelControl {SourceComponentIndex=original.SourceComponentIndex,Width=original.Width,Height=original.Height,
                    Input=input is null?null:PanelInputBinding.FromDefinition(input)}};
            groups.Add(control);order.Add(control.Id);
            foreach(var part in parts)
            {
                var element=Panel2DDocumentStorage.ToStorageElement(PanelElementModelCloner.Clone(part,objectId:Guid.NewGuid().ToString("N")));
                element=WithLocalTransform(element,new PanelTransform(X:-original.X,Y:-original.Y),controlId)
                    with {SharedSourceSetId=controlId,SharedSourceSetCount=parts.Length};
                elements.Add(element);order.Add(element.ObjectId);
            }
            index=end;
        }
        // Sections organise consecutive logical controls, not individual lamp
        // channels. A section containing one object adds no useful hierarchy.
        var childCounts=groups.Select(g=>g.ParentId).Concat(elements.Select(e=>e.ParentId))
            .Where(id=>id is not null).GroupBy(id=>id!).ToDictionary(g=>g.Key,g=>g.Count());
        var singletons=groups.Where(g=>g.Control is null&&childCounts.GetValueOrDefault(g.Id)==1).Select(g=>g.Id).ToHashSet();
        groups=groups.Where(g=>!singletons.Contains(g.Id)).Select(g=>singletons.Contains(g.ParentId??"")?g with{ParentId=null}:g).ToList();
        elements=elements.Select(e=>singletons.Contains(e.ParentId??"")?e with{ParentId=null}:e).ToList();
        order.RemoveAll(singletons.Contains);
        return document with {Groups=[..document.Groups,..groups],Elements=[..document.Elements,..elements],HierarchyOrder=order.ToArray()};
    }
}
