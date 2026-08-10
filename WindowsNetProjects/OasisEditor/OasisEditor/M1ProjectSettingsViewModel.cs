using System.Collections.ObjectModel;
namespace OasisEditor;

public sealed class M1ProjectSettingsViewModel
{
    private readonly M1ProjectSettings _model; private readonly Action<M1ProjectSettings> _changed;
    public M1ProjectSettingsViewModel(M1ProjectSettings model, Action<M1ProjectSettings> changed) { _model=model; _changed=changed; ProgramRoms=new(model.ProgramRoms.Select(x=>new M1RomSettingsViewModel(x,Save))); SoundRoms=new(model.SoundRoms.Select(x=>new M1RomSettingsViewModel(x,Save))); Reels=new(model.Reels.Select(x=>new M1ReelSettingsViewModel(x,Save))); Dips=new(model.Dips.Select((x,i)=>new M1DipSettingsViewModel(i,x,Save))); Hoppers=model.Hoppers; _percentageKey=model.PercentageKey; _edcEnabled=model.EdcEnabled; }
    private int _percentageKey; private bool _edcEnabled;
    public ObservableCollection<M1RomSettingsViewModel> ProgramRoms { get; } public ObservableCollection<M1RomSettingsViewModel> SoundRoms { get; } public ObservableCollection<M1ReelSettingsViewModel> Reels { get; } public ObservableCollection<M1DipSettingsViewModel> Dips { get; } public List<M1HopperSettings> Hoppers { get; }
    public IReadOnlyList<string> PercentageChoices { get; }=Enumerable.Range(0,16).Select(i=>$"{68+i*2}%").ToArray();
    public int PercentageKey { get=>_percentageKey; set { if(value==_percentageKey)return; _percentageKey=value; Save(); } } public bool EdcEnabled { get=>_edcEnabled; set { if(value==_edcEnabled)return; _edcEnabled=value; Save(); } }
    private void Save(){ _model.ProgramRoms=ProgramRoms.Select(x=>x.ToModel()).ToList(); _model.SoundRoms=SoundRoms.Select(x=>x.ToModel()).ToList(); _model.Reels=Reels.Select(x=>x.ToModel()).ToList(); _model.Dips=Dips.Select(x=>x.IsEnabled).ToList(); _model.PercentageKey=PercentageKey; _model.EdcEnabled=EdcEnabled; _changed(_model); }
}
public sealed class M1RomSettingsViewModel(M1RomSettings model,Action changed):NotifyAndSaveViewModel(changed){private string _path=model.Path;public int Slot{get;}=model.Slot;public string Path{get=>_path;set=>SetAndSave(ref _path,value??string.Empty);}public M1RomSettings ToModel()=>new(){Slot=Slot,Path=Path};}
public sealed class M1ReelSettingsViewModel(M1ReelSettings m,Action changed):NotifyAndSaveViewModel(changed){private int _steps=m.Steps,_start=m.OptoStart,_end=m.OptoEnd;private bool _invert=m.OptoInvert;public int ReelIndex{get;}=m.ReelIndex;public int Steps{get=>_steps;set=>SetAndSave(ref _steps,value);}public int OptoStart{get=>_start;set=>SetAndSave(ref _start,value);}public int OptoEnd{get=>_end;set=>SetAndSave(ref _end,value);}public bool OptoInvert{get=>_invert;set=>SetAndSave(ref _invert,value);}public M1ReelSettings ToModel()=>new(){ReelIndex=ReelIndex,Steps=Steps,OptoStart=OptoStart,OptoEnd=OptoEnd,OptoInvert=OptoInvert};}
public sealed class M1DipSettingsViewModel(int index,bool enabled,Action changed):NotifyAndSaveViewModel(changed){private bool _enabled=enabled;public int Index{get;}=index;public int DisplayNumber=>Index+1;public bool IsEnabled{get=>_enabled;set=>SetAndSave(ref _enabled,value);}}
