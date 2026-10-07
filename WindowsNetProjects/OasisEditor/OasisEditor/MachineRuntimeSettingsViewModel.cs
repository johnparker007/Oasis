using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Win32;

namespace OasisEditor;

/// <summary>Document-scoped editor for the owning Machine's authored emulation runtime.</summary>
public sealed class MachineRuntimeSettingsViewModel : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _owner;
    private bool _refreshing;
    private string _selectedTab = "ROMS";
    private System6NativeRomSettings _impact = new();
    private Mpu5NativeRomSettings _mpu5 = new();

    public MachineRuntimeSettingsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        BrowseSystem6ProgramRom1Command = BrowseCommand(1, true, false); BrowseSystem6ProgramRom2Command = BrowseCommand(2, true, false);
        BrowseSystem6ProgramRom3Command = BrowseCommand(3, true, false); BrowseSystem6ProgramRom4Command = BrowseCommand(4, true, false);
        BrowseSystem6SoundRom1Command = BrowseCommand(1, false, false); BrowseSystem6SoundRom2Command = BrowseCommand(2, false, false);
        BrowseSystem6SoundRom3Command = BrowseCommand(3, false, false); BrowseSystem6SoundRom4Command = BrowseCommand(4, false, false);
        BrowseMpu5ProgramRom1Command = BrowseCommand(1, true, true); BrowseMpu5ProgramRom2Command = BrowseCommand(2, true, true);
        BrowseMpu5ProgramRom3Command = BrowseCommand(3, true, true); BrowseMpu5ProgramRom4Command = BrowseCommand(4, true, true);
        BrowseMpu5SoundRom1Command = BrowseCommand(1, false, true); BrowseMpu5SoundRom2Command = BrowseCommand(2, false, true);
        BrowseMpu5SoundRom3Command = BrowseCommand(3, false, true); BrowseMpu5SoundRom4Command = BrowseCommand(4, false, true);
        ResetSystem6ReelOptosCommand = new RelayCommand(() => { System6ReelOptos = Rows(System6NativeRomSettings.CreateDefaultReelOptos()); SaveImpact(); });
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public FruitMachinePlatformType Platform => _owner.MachinePlatform;
    public IReadOnlyList<string> RuntimeSettingsTabs { get; } = ["ROMS", "Stake/Prize", "Reels", "Coins"];
    public string SelectedRuntimeSettingsTab { get => _selectedTab; set => Set(ref _selectedTab, value); }
    public Mpu5ProjectSettingsViewModel? Mpu5ProjectSettings { get; private set; }
    public EpochProjectSettingsViewModel? EpochProjectSettings { get; private set; }
    public Mpu3ProjectSettingsViewModel? Mpu3ProjectSettings { get; private set; }
    public M1ProjectSettingsViewModel? M1ProjectSettings { get; private set; }
    public Scorpion4ProjectSettingsViewModel? Scorpion4ProjectSettings { get; private set; }
    public ObservableCollection<System6ReelOptoSettingsViewModel> System6ReelOptos { get; private set; } = [];
    public ObservableCollection<System6CoinSettingsViewModel> System6Coins { get; private set; } = [];
    public IReadOnlyList<Mpu5CoinCommunicationStyle> Mpu5CoinCommunicationStyles { get; } = Enum.GetValues<Mpu5CoinCommunicationStyle>();
    public IReadOnlyList<Mpu5PicMode> Mpu5PicModes { get; } = Enum.GetValues<Mpu5PicMode>();
    public IReadOnlyList<Mpu5HopperType> Mpu5HopperTypes { get; } = Enum.GetValues<Mpu5HopperType>();
    public IReadOnlyList<Mpu5ReelJumperProfile> Mpu5ReelJumperProfiles { get; } = Enum.GetValues<Mpu5ReelJumperProfile>();
    public IReadOnlyList<EpochCoinCommunicationStyle> EpochCoinCommunicationStyles { get; } = Enum.GetValues<EpochCoinCommunicationStyle>();

    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var settings = (_owner.GetMachineDocument().Runtime as EmulationRuntimeDefinition)?.PlatformSettings;
            if (settings is System6NativeRomSettings impact)
            {
                _impact = Clone(impact); System6ReelOptos = Rows(_impact.ReelOptos); System6Coins = Coins(_impact.Coins);
            }
            if (settings is Mpu5NativeRomSettings mpu5) { _mpu5 = Clone(mpu5); Mpu5ProjectSettings = new(_mpu5, CommitMpu5); }
            EpochProjectSettings = settings is EpochNativeRomSettings epoch ? new(Clone(epoch), value => Commit(FruitMachinePlatformType.Epoch, value, "Edit Epoch runtime settings")) : null;
            Mpu3ProjectSettings = settings is Mpu3ProjectSettings mpu3 ? new(Clone(mpu3), value => Commit(FruitMachinePlatformType.MPU3, value, "Edit MPU3 runtime settings")) : null;
            M1ProjectSettings = settings is M1ProjectSettings m1 ? new(Clone(m1), value => Commit(FruitMachinePlatformType.MaygayM1, value, "Edit Maygay M1 runtime settings")) : null;
            Scorpion4ProjectSettings = settings is Scorpion4ProjectSettings scorpion ? new(Clone(scorpion), value => Commit(FruitMachinePlatformType.Scorpion4, value, "Edit Scorpion 4 runtime settings")) : null;
            NotifyAll();
        }
        finally { _refreshing = false; }
    }

    private void Commit<T>(FruitMachinePlatformType platform, T value, string description) where T : class
    {
        if (_owner.IsMachineEmulationRuntime)
            _owner.ExecuteMachineMutation(machine => machine with { Runtime = new EmulationRuntimeDefinition(platform, Clone(value)) }, description);
    }
    private void CommitMpu5(Mpu5NativeRomSettings value) => Commit(FruitMachinePlatformType.MPU5, value, "Edit MPU5 runtime settings");
    private void SaveImpact() { if (!_refreshing) Commit(FruitMachinePlatformType.Impact, BuildImpact(), "Edit Impact runtime settings"); }
    private System6NativeRomSettings BuildImpact() { _impact.ReelOptos = System6ReelOptos.Select(x => x.ToModel()).ToList(); _impact.Coins = System6Coins.Select(x => x.ToModel()).ToList(); return Clone(_impact); }
    private ObservableCollection<System6ReelOptoSettingsViewModel> Rows(IEnumerable<System6ReelOptoSettings> rows) => new(rows.Select(x => new System6ReelOptoSettingsViewModel(x, SaveImpact)));
    private ObservableCollection<System6CoinSettingsViewModel> Coins(IEnumerable<System6CoinSettings> rows) => new(rows.Select(x => new System6CoinSettingsViewModel(x, SaveImpact)));

    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)) ?? throw new InvalidOperationException("Unable to clone runtime settings.");
    private void SetImpact<T>(T value, Action<T> assign, [CallerMemberName] string name = "") { if (_refreshing) return; assign(value); PropertyChanged?.Invoke(this, new(name)); SaveImpact(); }
    private void SetMpu5Path(string value, Action<string> assign, [CallerMemberName] string name = "") { if (_refreshing) return; assign(value ?? string.Empty); PropertyChanged?.Invoke(this, new(name)); CommitMpu5(_mpu5); }

    public string System6ProgramRom1Path { get=>_impact.ProgramRom1Path; set=>SetImpact(value,v=>_impact.ProgramRom1Path=v); } public string System6ProgramRom2Path { get=>_impact.ProgramRom2Path; set=>SetImpact(value,v=>_impact.ProgramRom2Path=v); }
    public string System6ProgramRom3Path { get=>_impact.ProgramRom3Path; set=>SetImpact(value,v=>_impact.ProgramRom3Path=v); } public string System6ProgramRom4Path { get=>_impact.ProgramRom4Path; set=>SetImpact(value,v=>_impact.ProgramRom4Path=v); }
    public string System6SoundRom1Path { get=>_impact.SoundRom1Path; set=>SetImpact(value,v=>_impact.SoundRom1Path=v); } public string System6SoundRom2Path { get=>_impact.SoundRom2Path; set=>SetImpact(value,v=>_impact.SoundRom2Path=v); }
    public string System6SoundRom3Path { get=>_impact.SoundRom3Path; set=>SetImpact(value,v=>_impact.SoundRom3Path=v); } public string System6SoundRom4Path { get=>_impact.SoundRom4Path; set=>SetImpact(value,v=>_impact.SoundRom4Path=v); }
    public bool System6FlashSwitch { get=>_impact.FlashSwitch; set=>SetImpact(value,v=>_impact.FlashSwitch=v); } public int System6PercentSwitchValue { get=>_impact.PercentSwitchValue; set=>SetImpact(Math.Clamp(value,0,15),v=>_impact.PercentSwitchValue=v); }
    public AmberCoinCommunicationStyle System6CoinCommunicationStyle { get=>_impact.CoinCommunicationStyle; set=>SetImpact(value,v=>_impact.CoinCommunicationStyle=v); } public bool System6CoinCommunicationInvert { get=>_impact.CoinCommunicationInvert; set=>SetImpact(value,v=>_impact.CoinCommunicationInvert=v); }
    public uint System6CoinPulseCycles { get=>_impact.CoinPulseCycles; set=>SetImpact(value,v=>_impact.CoinPulseCycles=v); } public bool System6CoinEdcEnabled { get=>_impact.CoinEdcEnabled; set=>SetImpact(value,v=>_impact.CoinEdcEnabled=v); }
    public string System6NativeRomStatus => string.IsNullOrWhiteSpace(_impact.ProgramRom1Path) || string.IsNullOrWhiteSpace(_impact.ProgramRom2Path) ? "Program ROMs 1 and 2 are required." : "Configured; paths are validated when emulation starts.";

    public string Mpu5ProgramRom1Path { get=>_mpu5.ProgramRom1Path; set=>SetMpu5Path(value,v=>_mpu5.ProgramRom1Path=v); } public string Mpu5ProgramRom2Path { get=>_mpu5.ProgramRom2Path; set=>SetMpu5Path(value,v=>_mpu5.ProgramRom2Path=v); }
    public string Mpu5ProgramRom3Path { get=>_mpu5.ProgramRom3Path; set=>SetMpu5Path(value,v=>_mpu5.ProgramRom3Path=v); } public string Mpu5ProgramRom4Path { get=>_mpu5.ProgramRom4Path; set=>SetMpu5Path(value,v=>_mpu5.ProgramRom4Path=v); }
    public string Mpu5SoundRom1Path { get=>_mpu5.SoundRom1Path; set=>SetMpu5Path(value,v=>_mpu5.SoundRom1Path=v); } public string Mpu5SoundRom2Path { get=>_mpu5.SoundRom2Path; set=>SetMpu5Path(value,v=>_mpu5.SoundRom2Path=v); }
    public string Mpu5SoundRom3Path { get=>_mpu5.SoundRom3Path; set=>SetMpu5Path(value,v=>_mpu5.SoundRom3Path=v); } public string Mpu5SoundRom4Path { get=>_mpu5.SoundRom4Path; set=>SetMpu5Path(value,v=>_mpu5.SoundRom4Path=v); }
    public string Mpu5NativeRomStatus => string.IsNullOrWhiteSpace(_mpu5.ProgramRom1Path) ? "Program ROM 1 is required." : "Configured; paths are validated when emulation starts.";

    public ICommand BrowseSystem6ProgramRom1Command { get; } public ICommand BrowseSystem6ProgramRom2Command { get; } public ICommand BrowseSystem6ProgramRom3Command { get; } public ICommand BrowseSystem6ProgramRom4Command { get; }
    public ICommand BrowseSystem6SoundRom1Command { get; } public ICommand BrowseSystem6SoundRom2Command { get; } public ICommand BrowseSystem6SoundRom3Command { get; } public ICommand BrowseSystem6SoundRom4Command { get; }
    public ICommand BrowseMpu5ProgramRom1Command { get; } public ICommand BrowseMpu5ProgramRom2Command { get; } public ICommand BrowseMpu5ProgramRom3Command { get; } public ICommand BrowseMpu5ProgramRom4Command { get; }
    public ICommand BrowseMpu5SoundRom1Command { get; } public ICommand BrowseMpu5SoundRom2Command { get; } public ICommand BrowseMpu5SoundRom3Command { get; } public ICommand BrowseMpu5SoundRom4Command { get; }
    public ICommand ResetSystem6ReelOptosCommand { get; }
    private ICommand BrowseCommand(int slot, bool program, bool mpu5) => new RelayCommand(() => Browse(slot, program, mpu5));
    private void Browse(int slot, bool program, bool mpu5)
    {
        var dialog = new OpenFileDialog { Title=$"Select {(mpu5 ? "MPU5" : "Impact")} {(program ? "Program" : "Sound")} ROM {slot}", Filter="ROM files|*.bin;*.rom;*.p1;*.p2;*.p3;*.p4;*.snd|All files|*.*", InitialDirectory=_owner.ProjectDirectory, CheckFileExists=true };
        if (dialog.ShowDialog() != true) return;
        var path = _owner.ProjectDirectory is { } root ? Path.GetRelativePath(root, dialog.FileName).Replace('\\','/') : dialog.FileName;
        if (mpu5) SetMpu5Browse(slot, program, path); else SetImpactBrowse(slot, program, path);
    }
    internal void SetImpactBrowse(int slot, bool program, string path) { if(program){if(slot==1)System6ProgramRom1Path=path;else if(slot==2)System6ProgramRom2Path=path;else if(slot==3)System6ProgramRom3Path=path;else System6ProgramRom4Path=path;}else{if(slot==1)System6SoundRom1Path=path;else if(slot==2)System6SoundRom2Path=path;else if(slot==3)System6SoundRom3Path=path;else System6SoundRom4Path=path;} }
    internal void SetMpu5Browse(int slot, bool program, string path) { if(program){if(slot==1)Mpu5ProgramRom1Path=path;else if(slot==2)Mpu5ProgramRom2Path=path;else if(slot==3)Mpu5ProgramRom3Path=path;else Mpu5ProgramRom4Path=path;}else{if(slot==1)Mpu5SoundRom1Path=path;else if(slot==2)Mpu5SoundRom2Path=path;else if(slot==3)Mpu5SoundRom3Path=path;else Mpu5SoundRom4Path=path;} }
    private bool Set<T>(ref T field,T value,[CallerMemberName]string name="") { if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;PropertyChanged?.Invoke(this,new(name));return true; }
    private void NotifyAll() { foreach(var name in new[]{nameof(Platform),nameof(Mpu5ProjectSettings),nameof(EpochProjectSettings),nameof(Mpu3ProjectSettings),nameof(M1ProjectSettings),nameof(Scorpion4ProjectSettings),nameof(System6ReelOptos),nameof(System6Coins),nameof(System6ProgramRom1Path),nameof(System6ProgramRom2Path),nameof(System6ProgramRom3Path),nameof(System6ProgramRom4Path),nameof(System6SoundRom1Path),nameof(System6SoundRom2Path),nameof(System6SoundRom3Path),nameof(System6SoundRom4Path),nameof(System6FlashSwitch),nameof(System6PercentSwitchValue),nameof(System6CoinCommunicationStyle),nameof(System6CoinCommunicationInvert),nameof(System6CoinPulseCycles),nameof(System6CoinEdcEnabled),nameof(System6NativeRomStatus),nameof(Mpu5ProgramRom1Path),nameof(Mpu5ProgramRom2Path),nameof(Mpu5ProgramRom3Path),nameof(Mpu5ProgramRom4Path),nameof(Mpu5SoundRom1Path),nameof(Mpu5SoundRom2Path),nameof(Mpu5SoundRom3Path),nameof(Mpu5SoundRom4Path),nameof(Mpu5NativeRomStatus)}) PropertyChanged?.Invoke(this,new(name)); }
}
