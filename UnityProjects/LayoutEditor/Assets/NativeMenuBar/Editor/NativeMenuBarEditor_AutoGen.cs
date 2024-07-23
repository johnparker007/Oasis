
//
// MACHINE-GENERATED CODE - DO NOT MODIFY BY HAND!
//
// NOTE: You definitely SHOULD commit this file to source control!!!
//
// To regenerate this file, select MenuBar component, look
// in the custom Inspector window and press the Generate Editor menu.
//

using System.Linq;

public static class NativeMenuBar_AutoGen
{

    private static NativeMenuBar.Core.MenuBar menubar = UnityEngine.Object.FindObjectOfType<NativeMenuBar.Core.MenuBar>();
    [UnityEditor.MenuItem("NativeMenuBar/File/New Project")]
    private static void FileNewProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/New Project").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/New Project", true)]
    private static bool FileNewProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/New Project").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Open Project _O")]
    private static void FileOpenProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Open Project _O").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Open Project _O", true)]
    private static bool FileOpenProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Open Project _O").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Save Project _S")]
    private static void FileSaveProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Save Project _S").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Save Project _S", true)]
    private static bool FileSaveProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Save Project _S").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/-")]
    private static void File_0()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/-").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/-", true)]
    private static bool File_0Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/-").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Import MFME")]
    private static void FileImportMFME()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Import MFME").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Import MFME", true)]
    private static bool FileImportMFMEValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Import MFME").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Save Oasis Project")]
    private static void FileSaveOasisProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Save Oasis Project").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Save Oasis Project", true)]
    private static bool FileSaveOasisProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Save Oasis Project").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Export MAME")]
    private static void FileExportMAME()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Export MAME").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Export MAME", true)]
    private static bool FileExportMAMEValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Export MAME").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/---")]
    private static void File__1()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/---").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/---", true)]
    private static bool File__1Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/---").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Close")]
    private static void FileClose()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Close").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Close", true)]
    private static bool FileCloseValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Close").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Exit")]
    private static void FileExit()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Exit").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Exit", true)]
    private static bool FileExitValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Exit").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Undo _Z")]
    private static void EditUndo()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Undo _Z").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Undo _Z", true)]
    private static bool EditUndoValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Undo _Z").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Redo _Y")]
    private static void EditRedo()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Redo _Y").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Redo _Y", true)]
    private static bool EditRedoValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Redo _Y").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/-----")]
    private static void Edit___2()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/-----").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/-----", true)]
    private static bool Edit___2Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/-----").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Cut _X")]
    private static void EditCut()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Cut _X").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Cut _X", true)]
    private static bool EditCutValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Cut _X").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Copy _C")]
    private static void EditCopy()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Copy _C").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Copy _C", true)]
    private static bool EditCopyValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Copy _C").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Paste _V")]
    private static void EditPaste()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Paste _V").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Paste _V", true)]
    private static bool EditPasteValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Paste _V").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/-------")]
    private static void Edit____3()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/-------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/-------", true)]
    private static bool Edit____3Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/-------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Duplicate _D")]
    private static void EditDuplicate()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Duplicate _D").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Duplicate _D", true)]
    private static bool EditDuplicateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Duplicate _D").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Rename")]
    private static void EditRename()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Rename").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Rename", true)]
    private static bool EditRenameValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Rename").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Delete")]
    private static void EditDelete()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Delete").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Delete", true)]
    private static bool EditDeleteValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Delete").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/---------")]
    private static void Edit_____4()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/---------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/---------", true)]
    private static bool Edit_____4Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/---------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Preferences")]
    private static void EditPreferences()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Preferences").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Preferences", true)]
    private static bool EditPreferencesValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Preferences").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Lamp")]
    private static void ComponentLamp()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Lamp").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Lamp", true)]
    private static bool ComponentLampValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Lamp").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/LED")]
    private static void ComponentLED()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/LED").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/LED", true)]
    private static bool ComponentLEDValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/LED").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Reel")]
    private static void ComponentReel()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Reel").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Reel", true)]
    private static bool ComponentReelValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Reel").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Display")]
    private static void ComponentDisplay()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Display").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Display", true)]
    private static bool ComponentDisplayValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Display").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Button")]
    private static void ComponentButton()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Button").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Button", true)]
    private static bool ComponentButtonValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Button").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Screen")]
    private static void ComponentScreen()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Screen").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Screen", true)]
    private static bool ComponentScreenValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Screen").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Label")]
    private static void ComponentLabel()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Label").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Label", true)]
    private static bool ComponentLabelValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Label").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Image")]
    private static void ComponentImage()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Image").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Image", true)]
    private static bool ComponentImageValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Image").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/-----------")]
    private static void Component______5()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/-----------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/-----------", true)]
    private static bool Component______5Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/-----------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Group")]
    private static void ComponentGroup()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Group").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Group", true)]
    private static bool ComponentGroupValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Group").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start And Load State")]
    private static void EmulationStartAndLoadState()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start And Load State").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start And Load State", true)]
    private static bool EmulationStartAndLoadStateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start And Load State").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State And Exit")]
    private static void EmulationSaveStateAndExit()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State And Exit").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State And Exit", true)]
    private static bool EmulationSaveStateAndExitValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State And Exit").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/-------------")]
    private static void Emulation_______6()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/-------------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/-------------", true)]
    private static bool Emulation_______6Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/-------------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start")]
    private static void EmulationStart()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start", true)]
    private static bool EmulationStartValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Load State")]
    private static void EmulationLoadState()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Load State").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Load State", true)]
    private static bool EmulationLoadStateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Load State").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State")]
    private static void EmulationSaveState()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State", true)]
    private static bool EmulationSaveStateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Exit")]
    private static void EmulationExit()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Exit").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Exit", true)]
    private static bool EmulationExitValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Exit").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/---------------")]
    private static void Emulation________7()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/---------------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/---------------", true)]
    private static bool Emulation________7Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/---------------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Pause")]
    private static void EmulationPause()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Pause").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Pause", true)]
    private static bool EmulationPauseValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Pause").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Resume")]
    private static void EmulationResume()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Resume").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Resume", true)]
    private static bool EmulationResumeValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Resume").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/-----------------")]
    private static void Emulation_________8()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/-----------------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/-----------------", true)]
    private static bool Emulation_________8Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/-----------------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Throttle")]
    private static void EmulationThrottle()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Throttle").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Throttle", true)]
    private static bool EmulationThrottleValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Throttle").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Unthrottle")]
    private static void EmulationUnthrottle()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Unthrottle").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Unthrottle", true)]
    private static bool EmulationUnthrottleValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Unthrottle").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/-------------------")]
    private static void Emulation__________9()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/-------------------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/-------------------", true)]
    private static bool Emulation__________9Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/-------------------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Soft Reset")]
    private static void EmulationSoftReset()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Soft Reset").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Soft Reset", true)]
    private static bool EmulationSoftResetValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Soft Reset").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Hard Reset")]
    private static void EmulationHardReset()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Hard Reset").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Hard Reset", true)]
    private static bool EmulationHardResetValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Hard Reset").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Extract Layout")]
    private static void MFMEExtractLayout()
    {
        menubar.MenuItems.Single(item => item.FullPath == "MFME/Extract Layout").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Extract Layout", true)]
    private static bool MFMEExtractLayoutValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "MFME/Extract Layout").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Remap MPU4 Lamps")]
    private static void MFMERemapMPU4Lamps()
    {
        menubar.MenuItems.Single(item => item.FullPath == "MFME/Remap MPU4 Lamps").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Remap MPU4 Lamps", true)]
    private static bool MFMERemapMPU4LampsValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "MFME/Remap MPU4 Lamps").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode On")]
    private static void ViewDisplayTextModeOn()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode On").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode On", true)]
    private static bool ViewDisplayTextModeOnValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode On").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode Off")]
    private static void ViewDisplayTextModeOff()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode Off").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode Off", true)]
    private static bool ViewDisplayTextModeOffValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode Off").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Context Help")]
    private static void HelpContextHelp()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/Context Help").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Context Help", true)]
    private static bool HelpContextHelpValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/Context Help").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Manual")]
    private static void HelpManual()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/Manual").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Manual", true)]
    private static bool HelpManualValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/Manual").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/---------------------")]
    private static void Help___________10()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/---------------------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/---------------------", true)]
    private static bool Help___________10Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/---------------------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Check For Updates")]
    private static void HelpCheckForUpdates()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/Check For Updates").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Check For Updates", true)]
    private static bool HelpCheckForUpdatesValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/Check For Updates").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/-----------------------")]
    private static void Help____________11()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/-----------------------").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/-----------------------", true)]
    private static bool Help____________11Validate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/-----------------------").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/About")]
    private static void HelpAbout()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/About").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/About", true)]
    private static bool HelpAboutValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/About").IsInteractable;
    }

}
