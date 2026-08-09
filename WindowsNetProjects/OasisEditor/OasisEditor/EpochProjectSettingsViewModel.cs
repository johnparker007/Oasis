using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OasisEditor;

/// <summary>Independent, auto-saving editor projection of persisted Epoch settings.</summary>
public sealed class EpochProjectSettingsViewModel : INotifyPropertyChanged
{
    private readonly Action<EpochNativeRomSettings> _changed;
    private readonly EpochNativeRomSettings _model;

    public EpochProjectSettingsViewModel(EpochNativeRomSettings model, Action<EpochNativeRomSettings> changed)
    {
        _model = model;
        _changed = changed;
        Reels = new(model.Reels.Select(value => new EpochReelSettingsViewModel(value, Save)));
        Coins = new(model.Coins.Select(value => new EpochCoinChannelSettingsViewModel(value, Save)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<EpochReelSettingsViewModel> Reels { get; }
    public ObservableCollection<EpochCoinChannelSettingsViewModel> Coins { get; }
    public string ProgramRom1Path { get=>_model.ProgramRom1Path; set=>Set(value); }
    public string ProgramRom2Path { get=>_model.ProgramRom2Path; set=>Set(value); }
    public string ProgramRom3Path { get=>_model.ProgramRom3Path; set=>Set(value); }
    public string ProgramRom4Path { get=>_model.ProgramRom4Path; set=>Set(value); }
    public string SoundRom1Path { get=>_model.SoundRom1Path; set=>Set(value); }
    public string SoundRom2Path { get=>_model.SoundRom2Path; set=>Set(value); }
    public string SoundRom3Path { get=>_model.SoundRom3Path; set=>Set(value); }
    public string SoundRom4Path { get=>_model.SoundRom4Path; set=>Set(value); }
    public bool FlashRomMode { get=>_model.FlashRomMode; set=>Set(value); }
    public bool ConfigureReels { get=>_model.ConfigureReels; set=>Set(value); }
    public bool ApplyReelExt { get=>_model.ApplyReelExt; set=>Set(value); }
    public uint ReelExt { get=>_model.ReelExt; set=>Set(value); }
    public bool ConfigureCoins { get=>_model.ConfigureCoins; set=>Set(value); }
    public EpochCoinCommunicationStyle CommunicationStyle { get=>_model.CommunicationStyle; set=>Set(value); }
    public bool CommunicationInvert { get=>_model.CommunicationInvert; set=>Set(value); }
    public uint PulseCycles { get=>_model.PulseCycles; set=>Set(value); }
    public bool EdcEnabled { get=>_model.EdcEnabled; set=>Set(value); }
    public bool ConfigureMachineOptions { get=>_model.ConfigureMachineOptions; set=>Set(value); }
    public bool ApplyDips { get=>_model.ApplyDips; set=>Set(value); }
    public bool ApplyStake { get=>_model.ApplyStake; set=>Set(value); }
    public bool ApplyPrize { get=>_model.ApplyPrize; set=>Set(value); }
    public bool ApplyPercentage { get=>_model.ApplyPercentage; set=>Set(value); }
    public uint DipSwitchBits { get=>_model.DipSwitchBits; set=>Set(value); }
    public uint Stake { get=>_model.Stake; set=>Set(value); }
    public uint Prize { get=>_model.Prize; set=>Set(value); }
    public uint Percentage { get=>_model.Percentage; set=>Set(value); }

    public EpochNativeRomSettings ToModel() { _model.Reels=Reels.Select(x=>x.ToModel()).ToList(); _model.Coins=Coins.Select(x=>x.ToModel()).ToList(); return _model; }
    private void Set<T>(T value,[CallerMemberName]string name="") { var p=typeof(EpochNativeRomSettings).GetProperty(name)!; if(Equals(p.GetValue(_model),value))return; p.SetValue(_model,value); PropertyChanged?.Invoke(this,new(name)); Save(); }
    private void Save()=>_changed(ToModel());
}

public sealed class EpochReelSettingsViewModel(EpochReelSettings model,Action changed):NotifyAndSaveViewModel(changed)
{
    private bool _apply=model.Apply,_invert=model.OptoInvert; private int _steps=model.Steps,_start=model.OptoStart,_end=model.OptoEnd;
    public int ReelIndex { get; }=model.ReelIndex; public bool Apply {get=>_apply;set=>SetAndSave(ref _apply,value);} public int Steps {get=>_steps;set=>SetAndSave(ref _steps,value);} public int OptoStart {get=>_start;set=>SetAndSave(ref _start,value);} public int OptoEnd {get=>_end;set=>SetAndSave(ref _end,value);} public bool OptoInvert {get=>_invert;set=>SetAndSave(ref _invert,value);}
    public EpochReelSettings ToModel()=>new(){ReelIndex=ReelIndex,Apply=Apply,Steps=Steps,OptoStart=OptoStart,OptoEnd=OptoEnd,OptoInvert=OptoInvert};
}
public sealed class EpochCoinChannelSettingsViewModel(EpochCoinChannelSettings model,Action changed):NotifyAndSaveViewModel(changed)
{
    private bool _apply=model.Apply,_enabled=model.Enabled,_invert=model.LockoutInvert; private int _value=model.Value,_lockout=model.LockoutValue;
    public int Channel {get;}=model.Channel; public bool Apply {get=>_apply;set=>SetAndSave(ref _apply,value);} public bool Enabled {get=>_enabled;set=>SetAndSave(ref _enabled,value);} public int Value {get=>_value;set=>SetAndSave(ref _value,value);} public int LockoutValue {get=>_lockout;set=>SetAndSave(ref _lockout,value);} public bool LockoutInvert {get=>_invert;set=>SetAndSave(ref _invert,value);}
    public EpochCoinChannelSettings ToModel()=>new(){Channel=Channel,Apply=Apply,Enabled=Enabled,Value=Value,LockoutValue=LockoutValue,LockoutInvert=LockoutInvert};
}
