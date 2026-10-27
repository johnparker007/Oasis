using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private string? _selectedReflectionId;
    private void RefreshCabinet3DInspector()
    {
        _inspector.Children.Clear();_fields.Clear();var document=Cabinets.Document;
        Heading("CABINET");Field("Model scale",document.Model.Scale);Field("Up axis",document.Model.UpAxis);
        _inspector.Children.Add(new TextBlock{Text=document.Model.Path,TextWrapping=TextWrapping.Wrap,Opacity=.7});
        _inspector.Children.Add(ActionButton("Apply model settings",()=>Try(()=>Cabinets.SetModel(Number("Model scale"),_fields["Up axis"].Text))));
        _inspector.Children.Add(ActionButton("Replace model…",async()=>await ImportCabinetModelDialogAsync()));
        if(_composition is not { } scene)return;
        var targetId=_selectedSceneNodeId?.StartsWith("target:",StringComparison.Ordinal)==true?_selectedSceneNodeId[7..]:null;
        var target=scene.CabinetFaceTargets.FirstOrDefault(t=>t.Id==targetId);
        if(target is not null)
        {
            Heading("FACE TARGET");_inspector.Children.Add(new TextBlock{Text=target.DisplayName,TextWrapping=TextWrapping.Wrap});
            var settings=document.GetSurfaceTargetSettings(target.Id);
            var side=new ComboBox{ItemsSource=new[]{"normal","inverted"},SelectedItem=settings.FrontSide};
            var rotation=new ComboBox{ItemsSource=new[]{0,90,180,270},SelectedItem=settings.FaceRotation};
            var flip=new CheckBox{Content="Flip artwork horizontally",IsChecked=settings.FaceFlipHorizontal};
            AddLabeledControl("Front side",side);AddLabeledControl("Face rotation",rotation);_inspector.Children.Add(flip);
            _inspector.Children.Add(ActionButton("Apply Face orientation",()=>Try(()=>Cabinets.SetSurfaceTarget(new(target.Id,(string?)side.SelectedItem??"normal",(int?)rotation.SelectedItem??0,flip.IsChecked==true)))));
            if(!target.IsValid)_inspector.Children.Add(new TextBlock{Text=target.ErrorMessage,TextWrapping=TextWrapping.Wrap});
        }
        Heading("FACE TARGETS");foreach(var face in scene.CabinetFaceTargets)
        {var id=face.Id;_inspector.Children.Add(ActionButton(face.DisplayName,()=>SelectSceneNode("target:"+id)));}
        Heading("TRIGGERS");foreach(var trigger in scene.Meshes.Where(m=>m.SemanticKind==CabinetSemanticGeometryKind.Trigger))
            _inspector.Children.Add(new TextBlock{Text=trigger.SemanticId??trigger.NodePath,TextWrapping=TextWrapping.Wrap});
        AddCabinetReflectionInspector(document,scene.ReflectionTargets);
    }
    private async Task ImportCabinetModelDialogAsync()
    {
        if(TopLevel.GetTopLevel(this)?.StorageProvider is not {CanOpen:true} storage)return;
        var files=await storage.OpenFilePickerAsync(new(){Title="Replace Cabinet GLB",FileTypeFilter=[new("GLB models"){Patterns=["*.glb"]}]});
        if(files.FirstOrDefault()?.TryGetLocalPath() is { } path)Try(()=>WithSelectedModelFile(path,source=>Cabinets.ImportModel(source)));
    }
    private void AddCabinetReflectionInspector(CabinetDocument document,IReadOnlyList<CabinetReflectionReceiverTarget> receivers)
    {
        Heading("REFLECTION RECEIVERS");
        var receiver=new ComboBox{ItemsSource=receivers.Select(r=>r.TargetPath).ToArray(),SelectedIndex=receivers.Count>0?0:-1};
        AddLabeledControl("Receiver target",receiver);
        _inspector.Children.Add(ActionButton("Add reflection",()=>Try(()=>
        {
            var selected=receivers.FirstOrDefault(r=>r.TargetPath==(string?)receiver.SelectedItem)??throw new InvalidOperationException("The GLB has no reflection receiver target.");
            var slot=selected.MaterialSlots.FirstOrDefault()??throw new InvalidOperationException("The receiver has no material slot.");
            var created=new CabinetReflectionDefinition("reflection_"+Guid.NewGuid().ToString("N"),selected.TargetPath,slot.Index,[],CabinetReflectionSettings.RoughPlastic);
            _selectedReflectionId=created.Id;Cabinets.SetReflections((document.Reflections??[]).Append(created));
        })));
        var definitions=document.Reflections??[];
        if(definitions.Length==0)return;
        var chooser=new ComboBox{ItemsSource=definitions.Select(r=>r.Id).ToArray(),SelectedItem=_selectedReflectionId??definitions[0].Id};
        _selectedReflectionId=(string?)chooser.SelectedItem;AddLabeledControl("Reflection",chooser);
        chooser.SelectionChanged+=(_,_)=>{_selectedReflectionId=(string?)chooser.SelectedItem;RefreshCabinet3DInspector();};
        var reflection=definitions.FirstOrDefault(r=>r.Id==_selectedReflectionId)??definitions[0];
        Field("Reflection ID",reflection.Id);Field("Target path",reflection.TargetId);Field("Material slot",reflection.MaterialSlot);Field("Visibility mask",reflection.VisibilityMask??"");
        var settings=reflection.Settings with{};var settingFields=BuildTypedFields(settings,0);foreach(var control in settingFields.Controls)_inspector.Children.Add(control);
        var sourceEditors=new List<Action>();var sources=reflection.Sources.ToList();
        for(var index=0;index<sources.Count;index++)
        {
            var sourceIndex=index;var source=sources[index];Heading("SOURCE "+(index+1));
            var faceSource=new ComboBox{ItemsSource=_composition!.CabinetFaceTargets.Where(t=>t.IsValid).Select(t=>t.Id).ToArray(),SelectedItem=source.SourceSurfaceTargetId};
            var mode=new ComboBox{ItemsSource=new[]{CabinetReflectionPlaneSource.Automatic,CabinetReflectionPlaneSource.Manual},SelectedItem=source.PlaneSource};
            AddLabeledControl("Source Face target",faceSource);AddLabeledControl("Plane",mode);
            var plane=source.Plane with{Origin=source.Plane.Origin with{},Right=source.Plane.Right with{},Up=source.Plane.Up with{},Normal=source.Plane.Normal is null?null:source.Plane.Normal with{}};
            var edits=new[]{BuildTypedFields(plane,0),BuildTypedFields(plane.Origin,0),BuildTypedFields(plane.Right,0),BuildTypedFields(plane.Up,0)};
            foreach(var pair in edits.Select((e,i)=>(e,i)))
            {Heading(new[]{"PLANE SIZE","ORIGIN","RIGHT AXIS","UP AXIS"}[pair.i]);foreach(var control in pair.e.Controls)_inspector.Children.Add(control);}
            sourceEditors.Add(()=>
            {
                var id=(string?)faceSource.SelectedItem??source.SourceSurfaceTargetId;
                var planeSource=(string?)mode.SelectedItem??CabinetReflectionPlaneSource.Automatic;
                if(planeSource==CabinetReflectionPlaneSource.Automatic)
                {
                    var actual=_composition!.CabinetFaceTargets.FirstOrDefault(t=>t.Id==id);
                    if(!CabinetReflectionPlaneDeriver.TryDerive(actual,out plane,out var error))throw new InvalidOperationException(error);
                }
                else {edits[0].Apply(plane);edits[1].Apply(plane.Origin);edits[2].Apply(plane.Right);edits[3].Apply(plane.Up);}
                sources[sourceIndex]=new(id,plane,planeSource);
            });
            _inspector.Children.Add(ActionButton("Remove source",()=>Try(()=>Cabinets.SetReflections(definitions.Select(r=>r.Id==reflection.Id?r with{Sources=r.Sources.Where((_,i)=>i!=sourceIndex).ToArray()}:r)))));
        }
        _inspector.Children.Add(ActionButton("Add source Face target",()=>Try(()=>
        {
            if(sources.Count>=CabinetReflectionContract.MaximumSources)throw new InvalidOperationException("A receiver supports at most four source Faces.");
            var actual=_composition!.CabinetFaceTargets.FirstOrDefault(t=>t.IsValid)??throw new InvalidOperationException("No valid Face target is available.");
            if(!CabinetReflectionPlaneDeriver.TryDerive(actual,out var plane,out var error))throw new InvalidOperationException(error);
            Cabinets.SetReflections(definitions.Select(r=>r.Id==reflection.Id?r with{Sources=r.Sources.Append(new(actual.Id,plane)).ToArray()}:r));
        })));
        _inspector.Children.Add(ActionButton("Apply reflection",()=>Try(()=>
        {
            settingFields.Apply(settings);foreach(var edit in sourceEditors)edit();
            var updated=reflection with{Id=_fields["Reflection ID"].Text??"",TargetId=_fields["Target path"].Text??"",MaterialSlot=Integer("Material slot"),VisibilityMask=_fields["Visibility mask"].Text,Settings=settings,Sources=sources.ToArray()};
            _selectedReflectionId=updated.Id;Cabinets.SetReflections(definitions.Select(r=>r.Id==reflection.Id?updated:r));
        })));
        _inspector.Children.Add(ActionButton("Remove reflection",()=>Try(()=>Cabinets.SetReflections(definitions.Where(r=>r.Id!=reflection.Id)))));
    }
    private void RefreshMachine3DInspector()
    {
        var machine=Machines.Document;if(machine is null){RefreshMachineInspector();return;}
        var selected=machine.ObjectInstances.FirstOrDefault(i=>i.Id==_selectedSceneNodeId);
        var anchor=_selectedSceneNodeId?.StartsWith("anchor:",StringComparison.Ordinal)==true?machine.Anchors.FirstOrDefault(a=>a.Id==_selectedSceneNodeId[7..]):null;
        if(selected is null&&anchor is null)RefreshMachineInspector();
        else
        {
            _inspector.Children.Clear();_fields.Clear();Heading(anchor is null?"PLACED OBJECT":"ANCHOR");
            Field("Display name",selected?.DisplayName??anchor!.DisplayName);SceneTransformFields(selected?.Transform??new(anchor!.Position,anchor.Rotation,MachineVector3.One),anchor is null);
            var descendants = new HashSet<string>();
            if (selected is not null)
            {
                descendants.Add(selected.Id);
                bool added;
                do { added=false; foreach(var item in machine.ObjectInstances) if(item.ParentId is { } parentId && descendants.Contains(parentId)) added |= descendants.Add(item.Id); } while(added);
            }
            var active=new CheckBox{Content="Active",IsChecked=selected?.Active??true};var parent=new ComboBox{ItemsSource=new[]{""}.Concat(machine.ObjectInstances.Where(i=>!descendants.Contains(i.Id)).Select(i=>i.Id)).ToArray(),SelectedItem=selected?.ParentId??""};
            ComboBox? objectAsset=null;
            if(selected is not null)
            {
                _inspector.Children.Add(active);AddLabeledControl("Parent instance",parent);
                objectAsset=ReusableAssetCombo(EditorAssetType.Object3D,selected.ObjectAsset);
                AddLabeledControl("Prefab asset",objectAsset);
            }
            _inspector.Children.Add(ActionButton("Apply placement",()=>Try(()=>
            {
                var transform=ReadSceneTransform(anchor is null);
                Machines.UpdateDocument(current=>selected is not null?current with{ObjectInstances=current.ObjectInstances.Select(i=>i.Id==selected.Id?i with{DisplayName=_fields["Display name"].Text??i.DisplayName,Transform=transform,Active=active.IsChecked==true,ParentId=string.IsNullOrWhiteSpace((string?)parent.SelectedItem)?null:(string?)parent.SelectedItem,ObjectAsset=(objectAsset!.SelectedItem as ReusableAssetChoice)?.Reference}:i).ToArray()}
                    :current with{Anchors=current.Anchors.Select(a=>a.Id==anchor!.Id?a with{DisplayName=_fields["Display name"].Text??a.DisplayName,Position=transform.Position,Rotation=transform.Rotation}:a).ToArray()});
            })));
            _inspector.Children.Add(ActionButton("Remove selection",()=>Try(DeleteSceneSelection)));
        }
        Heading("COMPOSITION");_inspector.Children.Add(ActionButton("Machine settings",()=>SelectSceneNode(null)));
        var prefab=ReusableAssetCombo(EditorAssetType.Object3D);
        prefab.SelectedItem=(prefab.ItemsSource as IEnumerable<ReusableAssetChoice>)?.FirstOrDefault(choice=>choice.Reference is not null);
        AddLabeledControl("Prefab asset",prefab);
        _inspector.Children.Add(ActionButton("Add placed Prefab",()=>Try(()=>
        {
            var reference=(prefab.SelectedItem as ReusableAssetChoice)?.Reference??throw new InvalidOperationException("Create or import a Prefab before placing one.");
            var id="object_"+Guid.NewGuid().ToString("N");var displayName=Path.GetFileName(Path.GetDirectoryName(reference.Path));
            Machines.UpdateDocument(current=>current with{ObjectInstances=[..current.ObjectInstances,new(id,displayName,reference,MachineObjectTransform.Identity)]});SelectSceneNode(id);
        })));
        _inspector.Children.Add(ActionButton("Add anchor",()=>Try(()=>
        {var id="anchor_"+Guid.NewGuid().ToString("N");Machines.UpdateDocument(current=>current with{Anchors=[..current.Anchors,MachineAnchor.Create(id)]});SelectSceneNode("anchor:"+id);})));
    }
    private void SceneTransformFields(MachineObjectTransform transform,bool scale)
    {
        foreach(var (label,vector) in new[]{("Position",transform.Position),("Rotation",transform.Rotation)})
        {Field(label+" X",vector.X);Field(label+" Y",vector.Y);Field(label+" Z",vector.Z);}
        if(scale){Field("Scale X",transform.Scale.X);Field("Scale Y",transform.Scale.Y);Field("Scale Z",transform.Scale.Z);}
    }
    private MachineObjectTransform ReadSceneTransform(bool scale)=>new(new(Number("Position X"),Number("Position Y"),Number("Position Z")),new(Number("Rotation X"),Number("Rotation Y"),Number("Rotation Z")),scale?new(Number("Scale X"),Number("Scale Y"),Number("Scale Z")):MachineVector3.One);
    private void DeleteSceneSelection()
    {
        if(_sceneKind=="Prefab"&&_selectedSceneNodeId is { } node){Prefabs.RemoveNode(node);return;}
        if(_sceneKind!="Machine"||_selectedSceneNodeId is null)throw new InvalidOperationException("Choose a placed object, anchor or Prefab child to remove.");
        if(_selectedSceneNodeId.StartsWith("anchor:",StringComparison.Ordinal))Machines.UpdateDocument(current=>current with{Anchors=current.Anchors.Where(a=>a.Id!=_selectedSceneNodeId[7..]).ToArray()});
        else Machines.UpdateDocument(current=>
        {var ids=new HashSet<string>{_selectedSceneNodeId};bool changed;do{changed=false;foreach(var item in current.ObjectInstances)if(item.ParentId is { } parent&&ids.Contains(parent))changed|=ids.Add(item.Id);}while(changed);return current with{ObjectInstances=current.ObjectInstances.Where(i=>!ids.Contains(i.Id)).ToArray()};});
        SelectSceneNode(null);
    }
}
