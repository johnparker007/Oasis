using System.ComponentModel;
using ModelContextProtocol.Server;

namespace OasisEditor.Avalonia.Mcp;

public sealed partial class EditorTools
{
    [McpServerTool(Name="inspect_panel_control",ReadOnly=true),Description("Inspect one imported MFME control: its shared transform, persisted input and individual lamp channels. Counts use logical controls, not channel layers.")]
    public Task<object> InspectPanelControl(string id,CancellationToken cancellationToken)
        =>ui.Invoke<object>(()=>new {group=session.Groups.Single(g=>g.Id==id&&g.Control is not null),channels=session.ControlChannels(id)},cancellationToken);
    [McpServerTool(Name="update_panel_control_input"),Description("Undoably edit or disable an imported control's persisted input. Leaves lamp channels intact. Unspecified values retain decoded metadata; never invent coin channels or denominations.")]
    public Task<object> UpdatePanelControlInput(string id,CancellationToken cancellationToken,bool enabled=true,string? name=null,string? buttonNumber=null,
        string? keyboardShortcut=null,bool? inverted=null,bool? coinInput=null,int? coinChannel=null,int? coinValue=null,bool clearCoinChannel=false,bool clearCoinValue=false)
        =>ui.Invoke(()=>Act(()=>
        {
            var group=session.Groups.Single(g=>g.Id==id&&g.Control is not null);
            var binding=group.Control!.Input??new PanelInputBinding {Id=Guid.NewGuid().ToString("N"),Name=group.Name,Kind=InputDefinitionKind.Button};
            session.UpdateControlInput(id,enabled||group.Control.Input is not null?binding with {Enabled=enabled,Name=name??binding.Name,ButtonNumber=buttonNumber??binding.ButtonNumber,
                KeyboardShortcut=keyboardShortcut??binding.KeyboardShortcut,Inverted=inverted??binding.Inverted,CoinInput=coinInput??binding.CoinInput,
                Kind=coinInput.HasValue?coinInput.Value?InputDefinitionKind.Coin:InputDefinitionKind.Button:binding.Kind,
                CoinChannel=clearCoinChannel?null:coinChannel??binding.CoinChannel,CoinValue=clearCoinValue?null:coinValue??binding.CoinValue}:null);
        }),cancellationToken);
}
