using System.Collections.ObjectModel;

namespace OasisEditor;

public sealed class Scorpion4ProjectSettingsViewModel
{
    private readonly Scorpion4ProjectSettings _model; private readonly Action<Scorpion4ProjectSettings> _changed;
    private int _stake, _prize, _percentage, _hopperType; private bool _edcEnabled;
    public Scorpion4ProjectSettingsViewModel(Scorpion4ProjectSettings model, Action<Scorpion4ProjectSettings> changed)
    {
        _model=model; _changed=changed; ProgramRoms=new(model.ProgramRoms.Select(x=>new Scorpion4RomSettingsViewModel(x,Save))); SoundRoms=new(model.SoundRoms.Select(x=>new Scorpion4RomSettingsViewModel(x,Save)));
        Reels=new(model.Reels.Select(x=>new Scorpion4ReelSettingsViewModel(x,Save))); Dips=new(model.Dips.Select((x,i)=>new Scorpion4DipSettingsViewModel(i,x,Save)));
        Coins=new(model.Coins.Select(x=>new Scorpion4CoinSettingsViewModel(x,Save))); Hoppers=new(model.Hoppers.Select(x=>new Scorpion4HopperSettingsViewModel(x,Save)));
        _stake=model.Stake;_prize=model.Prize;_percentage=model.Percentage;_edcEnabled=model.EdcEnabled;_hopperType=model.HopperType;
    }
    public ObservableCollection<Scorpion4RomSettingsViewModel> ProgramRoms { get; } public ObservableCollection<Scorpion4RomSettingsViewModel> SoundRoms { get; }
    public ObservableCollection<Scorpion4ReelSettingsViewModel> Reels { get; } public ObservableCollection<Scorpion4DipSettingsViewModel> Dips { get; }
    public ObservableCollection<Scorpion4CoinSettingsViewModel> Coins { get; } public ObservableCollection<Scorpion4HopperSettingsViewModel> Hoppers { get; }
    public IReadOnlyList<string> StakeChoices { get; }=["None","5p","10p","20p","25p","30p","50p","£1"];
    public IReadOnlyList<string> PrizeChoices { get; }=["None","£3","£4","£6 cash","£6 token","£8 cash","£8 token","£10 cash","£5 cash","£15 cash","£25 cash","£25 LBO","£35","£70","£75","reserved"];
    public IReadOnlyList<string> HopperTypeChoices { get; }=["Compact","Universal","Empire Twin","Serial / ccTalk"];
    public int Stake {get=>_stake;set=>Set(ref _stake,value);} public int Prize {get=>_prize;set=>Set(ref _prize,value);} public int Percentage {get=>_percentage;set=>Set(ref _percentage,value);} public bool EdcEnabled {get=>_edcEnabled;set=>Set(ref _edcEnabled,value);} public int HopperType {get=>_hopperType;set=>Set(ref _hopperType,value);}
    private void Set<T>(ref T field,T value){if(EqualityComparer<T>.Default.Equals(field,value))return;field=value;Save();}
    private void Save(){_model.ProgramRoms=ProgramRoms.Select(x=>x.ToModel()).ToList();_model.SoundRoms=SoundRoms.Select(x=>x.ToModel()).ToList();_model.Reels=Reels.Select(x=>x.ToModel()).ToList();_model.Dips=Dips.OrderBy(x=>x.Index).Select(x=>x.IsEnabled).ToList();_model.Coins=Coins.Select(x=>x.ToModel()).ToList();_model.Hoppers=Hoppers.Select(x=>x.ToModel()).ToList();_model.Stake=Stake;_model.Prize=Prize;_model.Percentage=Percentage;_model.EdcEnabled=EdcEnabled;_model.HopperType=HopperType;_changed(_model);}
}
public sealed class Scorpion4RomSettingsViewModel(Scorpion4RomSettings m,Action save):NotifyAndSaveViewModel(save){private string _path=m.Path;public int Slot{get;}=m.Slot;public string Path{get=>_path;set=>SetAndSave(ref _path,value??string.Empty);}public Scorpion4RomSettings ToModel()=>new(){Slot=Slot,Path=Path};}
public sealed class Scorpion4ReelSettingsViewModel(Scorpion4ReelSettings m,Action save):NotifyAndSaveViewModel(save){private int _steps=m.Steps,_start=m.OptoStart,_end=m.OptoEnd;private bool _invert=m.OptoInvert;public int ReelIndex{get;}=m.ReelIndex;public int Steps{get=>_steps;set=>SetAndSave(ref _steps,value);}public int OptoStart{get=>_start;set=>SetAndSave(ref _start,value);}public int OptoEnd{get=>_end;set=>SetAndSave(ref _end,value);}public bool OptoInvert{get=>_invert;set=>SetAndSave(ref _invert,value);}public Scorpion4ReelSettings ToModel()=>new(){ReelIndex=ReelIndex,Steps=Steps,OptoStart=OptoStart,OptoEnd=OptoEnd,OptoInvert=OptoInvert};}
public sealed class Scorpion4DipSettingsViewModel(int index,bool value,Action save):NotifyAndSaveViewModel(save){private bool _enabled=value;public int Index{get;}=index;public int DisplayNumber=>Index+1;public bool IsEnabled{get=>_enabled;set=>SetAndSave(ref _enabled,value);}}
public sealed class Scorpion4CoinSettingsViewModel(Scorpion4CoinChannelSettings m,Action save):NotifyAndSaveViewModel(save){private bool _enabled=m.Enabled;private int _value=m.Value;public int ChannelIndex{get;}=m.ChannelIndex;public bool Enabled{get=>_enabled;set=>SetAndSave(ref _enabled,value);}public int Value{get=>_value;set=>SetAndSave(ref _value,value);}public Scorpion4CoinChannelSettings ToModel()=>new(){ChannelIndex=ChannelIndex,Enabled=Enabled,Value=Value};}
public sealed class Scorpion4HopperSettingsViewModel(Scorpion4HopperSettings m,Action save):NotifyAndSaveViewModel(save)
{private bool _enabled=m.Enabled,_lo=m.LoEnabled,_hi=m.HiEnabled;private int _coin=m.Coin;private uint _in=m.CoinsIn,_out=m.CoinsOut,_level=m.Level,_full=m.FullLevel,_loLevel=m.LoLevel,_hiLevel=m.HiLevel,_refilled=m.CoinsRefilled;public int HopperIndex{get;}=m.HopperIndex;public bool Enabled{get=>_enabled;set=>SetAndSave(ref _enabled,value);}public int Coin{get=>_coin;set=>SetAndSave(ref _coin,value);}public uint CoinsIn{get=>_in;set=>SetAndSave(ref _in,value);}public uint CoinsOut{get=>_out;set=>SetAndSave(ref _out,value);}public uint Level{get=>_level;set=>SetAndSave(ref _level,value);}public uint FullLevel{get=>_full;set=>SetAndSave(ref _full,value);}public bool LoEnabled{get=>_lo;set=>SetAndSave(ref _lo,value);}public uint LoLevel{get=>_loLevel;set=>SetAndSave(ref _loLevel,value);}public bool HiEnabled{get=>_hi;set=>SetAndSave(ref _hi,value);}public uint HiLevel{get=>_hiLevel;set=>SetAndSave(ref _hiLevel,value);}public uint CoinsRefilled{get=>_refilled;set=>SetAndSave(ref _refilled,value);}public Scorpion4HopperSettings ToModel()=>new(){HopperIndex=HopperIndex,Enabled=Enabled,Coin=Coin,CoinsIn=CoinsIn,CoinsOut=CoinsOut,Level=Level,FullLevel=FullLevel,LoEnabled=LoEnabled,LoLevel=LoLevel,HiEnabled=HiEnabled,HiLevel=HiLevel,CoinsRefilled=CoinsRefilled};}
