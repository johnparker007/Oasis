using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OasisEditor;

/// <summary>Editor-facing, auto-saving projection of the serialized MPU5 settings.</summary>
public sealed class Mpu5ProjectSettingsViewModel : INotifyPropertyChanged
{
    private readonly Action<Mpu5NativeRomSettings> _changed;
    private Mpu5NativeRomSettings _model;

    public Mpu5ProjectSettingsViewModel(Mpu5NativeRomSettings model, Action<Mpu5NativeRomSettings> changed)
    {
        _model = model;
        _changed = changed;
        Reels = new(model.Reels.Select(reel => new Mpu5ReelSettingsViewModel(reel, Save)));
        Coins = new(model.Coins.Select(coin => new Mpu5CoinChannelSettingsViewModel(coin, Save)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Mpu5ReelSettingsViewModel> Reels { get; }
    public ObservableCollection<Mpu5CoinChannelSettingsViewModel> Coins { get; }

    public bool ConfigureReels { get => _model.ConfigureReels; set => Set(value); }
    public bool ConfigureCoins { get => _model.ConfigureCoins; set => Set(value); }
    public Mpu5CoinCommunicationStyle CommunicationStyle { get => _model.CommunicationStyle; set => Set(value); }
    public bool CommunicationInvert { get => _model.CommunicationInvert; set => Set(value); }
    public uint PulseCycles { get => _model.PulseCycles; set => Set(value); }
    public bool EdcEnabled { get => _model.EdcEnabled; set => Set(value); }
    public bool ConfigureMachineOptions { get => _model.ConfigureMachineOptions; set => Set(value); }
    public bool ApplyDips { get => _model.ApplyDips; set => Set(value); }
    public bool ApplyStake { get => _model.ApplyStake; set => Set(value); }
    public bool ApplyPrize { get => _model.ApplyPrize; set => Set(value); }
    public bool ApplyPercentage { get => _model.ApplyPercentage; set => Set(value); }
    public bool ApplyCharacteriserAddress { get => _model.ApplyCharacteriserAddress; set => Set(value); }
    public bool ApplyPicMode { get => _model.ApplyPicMode; set => Set(value); }
    public bool ApplySecFitted { get => _model.ApplySecFitted; set => Set(value); }
    public bool ApplyHopperType { get => _model.ApplyHopperType; set => Set(value); }
    public bool ApplyReelJumperProfile0 { get => _model.ApplyReelJumperProfile0; set => Set(value); }
    public bool ApplyReelJumperProfile1 { get => _model.ApplyReelJumperProfile1; set => Set(value); }
    public uint DipSwitchBits { get => _model.DipSwitchBits; set => Set(value); }
    public uint Stake { get => _model.Stake; set => Set(value); }
    public uint Prize { get => _model.Prize; set => Set(value); }
    public uint Percentage { get => _model.Percentage; set => Set(value); }
    public uint CharacteriserAddress { get => _model.CharacteriserAddress; set => Set(value); }
    public Mpu5PicMode PicMode { get => _model.PicMode; set => Set(value); }
    public bool SecFitted { get => _model.SecFitted; set => Set(value); }
    public Mpu5HopperType HopperType { get => _model.HopperType; set => Set(value); }
    public Mpu5ReelJumperProfile ReelJumperProfile0 { get => _model.ReelJumperProfile0; set => Set(value); }
    public Mpu5ReelJumperProfile ReelJumperProfile1 { get => _model.ReelJumperProfile1; set => Set(value); }

    public Mpu5NativeRomSettings ToModel()
    {
        _model.Reels = Reels.Select(reel => reel.ToModel()).ToList();
        _model.Coins = Coins.Select(coin => coin.ToModel()).ToList();
        return _model;
    }

    private void Set<T>(T value, [CallerMemberName] string propertyName = "")
    {
        var property = typeof(Mpu5NativeRomSettings).GetProperty(propertyName)!;
        if (Equals(property.GetValue(_model), value)) return;
        property.SetValue(_model, value);
        PropertyChanged?.Invoke(this, new(propertyName));
        Save();
    }

    private void Save() => _changed(ToModel());
}

public sealed class Mpu5ReelSettingsViewModel(Mpu5ReelSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private bool _apply = model.Apply;
    private int _steps = model.Steps;
    private int _optoStart = model.OptoStart;
    private int _optoEnd = model.OptoEnd;
    private bool _optoInvert = model.OptoInvert;
    public int ReelIndex { get; } = model.ReelIndex;
    public bool Apply { get => _apply; set => SetAndSave(ref _apply, value); }
    public int Steps { get => _steps; set => SetAndSave(ref _steps, value); }
    public int OptoStart { get => _optoStart; set => SetAndSave(ref _optoStart, value); }
    public int OptoEnd { get => _optoEnd; set => SetAndSave(ref _optoEnd, value); }
    public bool OptoInvert { get => _optoInvert; set => SetAndSave(ref _optoInvert, value); }
    public Mpu5ReelSettings ToModel() => new() { ReelIndex = ReelIndex, Apply = Apply, Steps = Steps, OptoStart = OptoStart, OptoEnd = OptoEnd, OptoInvert = OptoInvert };
}

public sealed class Mpu5CoinChannelSettingsViewModel(Mpu5CoinChannelSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private bool _apply = model.Apply, _enabled = model.Enabled, _lockoutInvert = model.LockoutInvert;
    private int _value = model.Value;
    public int Channel { get; } = model.Channel;
    public bool Apply { get => _apply; set => SetAndSave(ref _apply, value); }
    public bool Enabled { get => _enabled; set => SetAndSave(ref _enabled, value); }
    public int Value { get => _value; set => SetAndSave(ref _value, value); }
    public bool LockoutInvert { get => _lockoutInvert; set => SetAndSave(ref _lockoutInvert, value); }
    public Mpu5CoinChannelSettings ToModel() => new() { Channel = Channel, Apply = Apply, Enabled = Enabled, Value = Value, LockoutInvert = LockoutInvert };
}

public abstract class NotifyAndSaveViewModel(Action changed) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void SetAndSave<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new(propertyName));
        changed();
    }
}
