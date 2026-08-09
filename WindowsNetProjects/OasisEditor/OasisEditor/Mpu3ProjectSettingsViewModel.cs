using System.Collections.ObjectModel;

namespace OasisEditor;

/// <summary>Editor-facing, auto-saving projection of the persisted MPU3 settings.</summary>
public sealed class Mpu3ProjectSettingsViewModel
{
    private readonly Mpu3ProjectSettings _model;
    private readonly Action<Mpu3ProjectSettings> _changed;

    public Mpu3ProjectSettingsViewModel(Mpu3ProjectSettings model, Action<Mpu3ProjectSettings> changed)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        ProgramRoms = new(model.ProgramRoms.Select(value => new Mpu3ProgramRomSettingsViewModel(value, Save)));
        Reels = new(model.Reels.Select(value => new Mpu3ReelSettingsViewModel(value, Save)));
        Dips = new(model.Dips.Select((value, index) => new Mpu3DipSettingsViewModel(index, value, Save)));
    }

    public ObservableCollection<Mpu3ProgramRomSettingsViewModel> ProgramRoms { get; }
    public ObservableCollection<Mpu3ReelSettingsViewModel> Reels { get; }
    public ObservableCollection<Mpu3DipSettingsViewModel> Dips { get; }

    public Mpu3ProjectSettings ToModel()
    {
        _model.ProgramRoms = ProgramRoms.Select(value => value.ToModel()).ToList();
        _model.Reels = Reels.Select(value => value.ToModel()).ToList();
        _model.Dips = Dips.OrderBy(value => value.Index).Select(value => value.IsEnabled).ToList();
        return _model;
    }

    private void Save() => _changed(ToModel());
}

public sealed class Mpu3ProgramRomSettingsViewModel(Mpu3ProgramRomSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private string _path = model.Path;

    public int Slot { get; } = model.Slot;
    public string Path { get => _path; set => SetAndSave(ref _path, value ?? string.Empty); }
    public Mpu3ProgramRomSettings ToModel() => new() { Slot = Slot, Path = Path };
}

public sealed class Mpu3ReelSettingsViewModel(Mpu3ReelSettings model, Action changed) : NotifyAndSaveViewModel(changed)
{
    private int _steps = model.Steps;
    private int _optoStart = model.OptoStart;
    private int _optoEnd = model.OptoEnd;
    private bool _optoInvert = model.OptoInvert;

    public int ReelIndex { get; } = model.ReelIndex;
    public int Steps { get => _steps; set => SetAndSave(ref _steps, value); }
    public int OptoStart { get => _optoStart; set => SetAndSave(ref _optoStart, value); }
    public int OptoEnd { get => _optoEnd; set => SetAndSave(ref _optoEnd, value); }
    public bool OptoInvert { get => _optoInvert; set => SetAndSave(ref _optoInvert, value); }
    public Mpu3ReelSettings ToModel() => new() { ReelIndex = ReelIndex, Steps = Steps, OptoStart = OptoStart, OptoEnd = OptoEnd, OptoInvert = OptoInvert };
}

public sealed class Mpu3DipSettingsViewModel(int index, bool isEnabled, Action changed) : NotifyAndSaveViewModel(changed)
{
    private bool _isEnabled = isEnabled;
    public int Index { get; } = index;
    public int DisplayNumber => Index + 1;
    public bool IsEnabled { get => _isEnabled; set => SetAndSave(ref _isEnabled, value); }
}
