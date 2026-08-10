using System.Collections.ObjectModel;

namespace OasisEditor;

/// <summary>Editor-facing, auto-saving projection of the persisted Maygay M1 settings.</summary>
public sealed class M1ProjectSettingsViewModel
{
    private readonly M1ProjectSettings _model;
    private readonly Action<M1ProjectSettings> _changed;
    private int _percentageKey;
    private bool _edcEnabled;

    public M1ProjectSettingsViewModel(M1ProjectSettings model, Action<M1ProjectSettings> changed)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        ProgramRoms = new(model.ProgramRoms.Select(value => new M1RomSettingsViewModel(value, Save)));
        SoundRoms = new(model.SoundRoms.Select(value => new M1RomSettingsViewModel(value, Save)));
        Reels = new(model.Reels.Select(value => new M1ReelSettingsViewModel(value, Save)));
        Dips = new(model.Dips.Select((value, index) => new M1DipSettingsViewModel(index, value, Save)));
        Hoppers = new(model.Hoppers.Select(value => new M1HopperSettingsViewModel(value, Save)));
        _percentageKey = model.PercentageKey;
        _edcEnabled = model.EdcEnabled;
    }

    public ObservableCollection<M1RomSettingsViewModel> ProgramRoms { get; }
    public ObservableCollection<M1RomSettingsViewModel> SoundRoms { get; }
    public ObservableCollection<M1ReelSettingsViewModel> Reels { get; }
    public ObservableCollection<M1DipSettingsViewModel> Dips { get; }
    public ObservableCollection<M1HopperSettingsViewModel> Hoppers { get; }
    public IReadOnlyList<string> PercentageChoices { get; } = Enumerable.Range(0, 16).Select(index => $"{68 + index * 2}%").ToArray();

    public int PercentageKey { get => _percentageKey; set { if (_percentageKey == value) return; _percentageKey = value; Save(); } }
    public bool EdcEnabled { get => _edcEnabled; set { if (_edcEnabled == value) return; _edcEnabled = value; Save(); } }

    private void Save()
    {
        _model.ProgramRoms = ProgramRoms.Select(value => value.ToModel()).ToList();
        _model.SoundRoms = SoundRoms.Select(value => value.ToModel()).ToList();
        _model.Reels = Reels.Select(value => value.ToModel()).ToList();
        _model.Dips = Dips.OrderBy(value => value.Index).Select(value => value.IsEnabled).ToList();
        _model.Hoppers = Hoppers.Select(value => value.ToModel()).ToList();
        _model.PercentageKey = PercentageKey;
        _model.EdcEnabled = EdcEnabled;
        _changed(_model);
    }
}

public sealed class M1RomSettingsViewModel(M1RomSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private string _path = model.Path;
    public int Slot { get; } = model.Slot;
    public string Path { get => _path; set => SetAndSave(ref _path, value ?? string.Empty); }
    public M1RomSettings ToModel() => new() { Slot = Slot, Path = Path };
}

public sealed class M1ReelSettingsViewModel(M1ReelSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private int _steps = model.Steps, _optoStart = model.OptoStart, _optoEnd = model.OptoEnd;
    private bool _optoInvert = model.OptoInvert;
    public int ReelIndex { get; } = model.ReelIndex;
    public int Steps { get => _steps; set => SetAndSave(ref _steps, value); }
    public int OptoStart { get => _optoStart; set => SetAndSave(ref _optoStart, value); }
    public int OptoEnd { get => _optoEnd; set => SetAndSave(ref _optoEnd, value); }
    public bool OptoInvert { get => _optoInvert; set => SetAndSave(ref _optoInvert, value); }
    public M1ReelSettings ToModel() => new() { ReelIndex = ReelIndex, Steps = Steps, OptoStart = OptoStart, OptoEnd = OptoEnd, OptoInvert = OptoInvert };
}

public sealed class M1DipSettingsViewModel(int index, bool enabled, Action changed) : NotifyAndSaveViewModel(changed)
{
    private bool _isEnabled = enabled;
    public int Index { get; } = index;
    public int DisplayNumber => Index + 1;
    public bool IsEnabled { get => _isEnabled; set => SetAndSave(ref _isEnabled, value); }
}

public sealed class M1HopperSettingsViewModel(M1HopperSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private bool _enabled=model.Enabled, _optoEnable=model.OptoEnable, _motorEnable=model.MotorEnable, _loEnable=model.LoEnable, _loInvert=model.LoInvert, _hiEnable=model.HiEnable, _hiInvert=model.HiInvert, _loIndicator=model.LoIndicator, _hiIndicator=model.HiIndicator;
    private int _optoReturn=model.OptoReturn, _coin=model.Coin, _loSwitch=model.LoSwitch, _hiSwitch=model.HiSwitch;
    private uint _coinsIn=model.CoinsIn, _coinsOut=model.CoinsOut, _level=model.Level, _fullLevel=model.FullLevel, _loLevel=model.LoLevel, _hiLevel=model.HiLevel, _coinsRefilled=model.CoinsRefilled;
    public int HopperIndex { get; } = model.HopperIndex;
    public bool Enabled { get=>_enabled; set=>SetAndSave(ref _enabled,value); } public uint CoinsIn { get=>_coinsIn; set=>SetAndSave(ref _coinsIn,value); } public uint CoinsOut { get=>_coinsOut; set=>SetAndSave(ref _coinsOut,value); }
    public bool OptoEnable { get=>_optoEnable; set=>SetAndSave(ref _optoEnable,value); } public int OptoReturn { get=>_optoReturn; set=>SetAndSave(ref _optoReturn,value); } public bool MotorEnable { get=>_motorEnable; set=>SetAndSave(ref _motorEnable,value); } public int Coin { get=>_coin; set=>SetAndSave(ref _coin,value); }
    public uint Level { get=>_level; set=>SetAndSave(ref _level,value); } public uint FullLevel { get=>_fullLevel; set=>SetAndSave(ref _fullLevel,value); } public bool LoEnable { get=>_loEnable; set=>SetAndSave(ref _loEnable,value); } public bool LoInvert { get=>_loInvert; set=>SetAndSave(ref _loInvert,value); }
    public int LoSwitch { get=>_loSwitch; set=>SetAndSave(ref _loSwitch,value); } public uint LoLevel { get=>_loLevel; set=>SetAndSave(ref _loLevel,value); } public bool HiEnable { get=>_hiEnable; set=>SetAndSave(ref _hiEnable,value); } public bool HiInvert { get=>_hiInvert; set=>SetAndSave(ref _hiInvert,value); }
    public int HiSwitch { get=>_hiSwitch; set=>SetAndSave(ref _hiSwitch,value); } public uint HiLevel { get=>_hiLevel; set=>SetAndSave(ref _hiLevel,value); } public bool LoIndicator { get=>_loIndicator; set=>SetAndSave(ref _loIndicator,value); } public bool HiIndicator { get=>_hiIndicator; set=>SetAndSave(ref _hiIndicator,value); } public uint CoinsRefilled { get=>_coinsRefilled; set=>SetAndSave(ref _coinsRefilled,value); }
    public M1HopperSettings ToModel() => new() { HopperIndex=HopperIndex, Enabled=Enabled, CoinsIn=CoinsIn, CoinsOut=CoinsOut, OptoEnable=OptoEnable, OptoReturn=OptoReturn, MotorEnable=MotorEnable, Coin=Coin, Level=Level, FullLevel=FullLevel, LoEnable=LoEnable, LoInvert=LoInvert, LoSwitch=LoSwitch, LoLevel=LoLevel, HiEnable=HiEnable, HiInvert=HiInvert, HiSwitch=HiSwitch, HiLevel=HiLevel, LoIndicator=LoIndicator, HiIndicator=HiIndicator, CoinsRefilled=CoinsRefilled };
}
