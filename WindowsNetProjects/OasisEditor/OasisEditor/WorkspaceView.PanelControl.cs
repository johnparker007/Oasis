using Avalonia.Controls;
using Avalonia.Media;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    public IReadOnlyList<InputDefinitionModel> RecoverablePanelInputs => DocumentHost._nativeDocuments.Values
        .Where(d=>d.Asset.Kind=="Panel2D"&&d.Asset.LibraryRoot is null).SelectMany(d=>d.Asset.Sessions.Project.ImportedInputDefinitions)
        .Concat(DocumentHost._session.ImportedInputDefinitions).DistinctBy(i=>i.Id).ToArray();
    private void AppendControlInspector(PanelGroup group)
    {
        var channels=_session.ControlChannels(group.Id);
        var list=new StackPanel {Spacing=6};
        list.Children.Add(new TextBlock {Text="Each channel keeps its own lamp output, image and mask.",TextWrapping=TextWrapping.Wrap});
        foreach(var channel in channels)
        {
            var lamp=_session.RenderDocument!.Elements.Single(e=>e.ObjectId==channel.Id);
            list.Children.Add(ActionButton($"Lamp {lamp.DisplayNumber?.ToString()??"unassigned"} — edit channel",()=>Try(()=>
            {SetPanelGroupExpanded(group.Id,true);_session.Select(channel.Id);})));
        }
        _inspector.Children.Add(new Expander {Name="ControlLampChannels",Header=$"Lamp channels ({channels.Count})",Content=list});
        Heading("INPUT");
        var binding=group.Control!.Input;
        var enabled=new CheckBox {Content="Enable input",IsChecked=binding?.Enabled==true};_inspector.Children.Add(enabled);
        Field("Input name",binding?.Name??group.Name);Field("Button",binding?.ButtonNumber??"");Field("Shortcut",binding?.KeyboardShortcut??"");
        var coin=new CheckBox {Content="Coin input",IsChecked=binding?.CoinInput==true};_inspector.Children.Add(coin);
        var inverted=new CheckBox {Content="Inverted",IsChecked=binding?.Inverted==true};_inspector.Children.Add(inverted);
        _inspector.Children.Add(ActionButton("Apply input",()=>Try(()=>
        {
            var next=enabled.IsChecked==true||binding is not null?(binding??new PanelInputBinding {Id=Guid.NewGuid().ToString("N")}) with
            {Enabled=enabled.IsChecked==true,Name=_fields["Input name"].Text??"",ButtonNumber=_fields["Button"].Text??"",KeyboardShortcut=_fields["Shortcut"].Text??"",
                CoinInput=coin.IsChecked==true,Kind=coin.IsChecked==true?InputDefinitionKind.Coin:InputDefinitionKind.Button,Inverted=inverted.IsChecked==true}:null;
            _session.UpdateControlInput(group.Id,next);
        })));
        if(!string.IsNullOrWhiteSpace(binding?.Notes))_inspector.Children.Add(new TextBlock {Text=binding.Notes,TextWrapping=TextWrapping.Wrap});
    }
}
