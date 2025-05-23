
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
    [UnityEditor.MenuItem("NativeMenuBar/File/New Project", priority = 0)]
    private static void FileNewProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/New Project").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/New Project", true, 0)]
    private static bool FileNewProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/New Project").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Open Project", priority = 1)]
    private static void FileOpenProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Open Project").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Open Project", true, 1)]
    private static bool FileOpenProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Open Project").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Save Project", priority = 2)]
    private static void FileSaveProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Save Project").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Save Project", true, 2)]
    private static bool FileSaveProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Save Project").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Import MFME", priority = 13)]
    private static void FileImportMFME()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Import MFME").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Import MFME", true, 13)]
    private static bool FileImportMFMEValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Import MFME").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Export MAME", priority = 14)]
    private static void FileExportMAME()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Export MAME").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Export MAME", true, 14)]
    private static bool FileExportMAMEValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Export MAME").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Close", priority = 25)]
    private static void FileClose()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Close").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Close", true, 25)]
    private static bool FileCloseValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Close").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Exit", priority = 26)]
    private static void FileExit()
    {
        menubar.MenuItems.Single(item => item.FullPath == "File/Exit").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/File/Exit", true, 26)]
    private static bool FileExitValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "File/Exit").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Undo", priority = 27)]
    private static void EditUndo()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Undo").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Undo", true, 27)]
    private static bool EditUndoValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Undo").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Redo", priority = 28)]
    private static void EditRedo()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Redo").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Redo", true, 28)]
    private static bool EditRedoValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Redo").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Cut", priority = 39)]
    private static void EditCut()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Cut").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Cut", true, 39)]
    private static bool EditCutValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Cut").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Copy", priority = 40)]
    private static void EditCopy()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Copy").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Copy", true, 40)]
    private static bool EditCopyValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Copy").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Paste", priority = 41)]
    private static void EditPaste()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Paste").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Paste", true, 41)]
    private static bool EditPasteValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Paste").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Duplicate", priority = 52)]
    private static void EditDuplicate()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Duplicate").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Duplicate", true, 52)]
    private static bool EditDuplicateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Duplicate").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Rename", priority = 53)]
    private static void EditRename()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Rename").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Rename", true, 53)]
    private static bool EditRenameValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Rename").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Delete", priority = 54)]
    private static void EditDelete()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Delete").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Delete", true, 54)]
    private static bool EditDeleteValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Delete").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Project Settings", priority = 65)]
    private static void EditProjectSettings()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Project Settings").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Project Settings", true, 65)]
    private static bool EditProjectSettingsValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Project Settings").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Preferences", priority = 66)]
    private static void EditPreferences()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Edit/Preferences").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Edit/Preferences", true, 66)]
    private static bool EditPreferencesValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Edit/Preferences").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Lamp", priority = 67)]
    private static void ComponentLamp()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Lamp").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Lamp", true, 67)]
    private static bool ComponentLampValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Lamp").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/LED", priority = 68)]
    private static void ComponentLED()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/LED").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/LED", true, 68)]
    private static bool ComponentLEDValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/LED").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Reel", priority = 69)]
    private static void ComponentReel()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Reel").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Reel", true, 69)]
    private static bool ComponentReelValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Reel").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Display", priority = 70)]
    private static void ComponentDisplay()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Display").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Display", true, 70)]
    private static bool ComponentDisplayValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Display").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Button", priority = 71)]
    private static void ComponentButton()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Button").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Button", true, 71)]
    private static bool ComponentButtonValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Button").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Screen", priority = 72)]
    private static void ComponentScreen()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Screen").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Screen", true, 72)]
    private static bool ComponentScreenValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Screen").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Label", priority = 73)]
    private static void ComponentLabel()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Label").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Label", true, 73)]
    private static bool ComponentLabelValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Label").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Image", priority = 74)]
    private static void ComponentImage()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Image").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Image", true, 74)]
    private static bool ComponentImageValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Image").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Group", priority = 85)]
    private static void ComponentGroup()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Component/Group").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Component/Group", true, 85)]
    private static bool ComponentGroupValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Component/Group").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start And Load State", priority = 86)]
    private static void EmulationStartAndLoadState()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start And Load State").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start And Load State", true, 86)]
    private static bool EmulationStartAndLoadStateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start And Load State").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State And Exit", priority = 87)]
    private static void EmulationSaveStateAndExit()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State And Exit").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State And Exit", true, 87)]
    private static bool EmulationSaveStateAndExitValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State And Exit").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start", priority = 98)]
    private static void EmulationStart()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Start", true, 98)]
    private static bool EmulationStartValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Start").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Load State", priority = 99)]
    private static void EmulationLoadState()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Load State").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Load State", true, 99)]
    private static bool EmulationLoadStateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Load State").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State", priority = 100)]
    private static void EmulationSaveState()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Save State", true, 100)]
    private static bool EmulationSaveStateValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Save State").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Exit", priority = 101)]
    private static void EmulationExit()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Exit").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Exit", true, 101)]
    private static bool EmulationExitValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Exit").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Pause", priority = 112)]
    private static void EmulationPause()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Pause").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Pause", true, 112)]
    private static bool EmulationPauseValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Pause").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Resume", priority = 113)]
    private static void EmulationResume()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Resume").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Resume", true, 113)]
    private static bool EmulationResumeValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Resume").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Throttle", priority = 124)]
    private static void EmulationThrottle()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Throttle").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Throttle", true, 124)]
    private static bool EmulationThrottleValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Throttle").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Unthrottle", priority = 125)]
    private static void EmulationUnthrottle()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Unthrottle").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Unthrottle", true, 125)]
    private static bool EmulationUnthrottleValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Unthrottle").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Soft Reset", priority = 136)]
    private static void EmulationSoftReset()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Soft Reset").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Soft Reset", true, 136)]
    private static bool EmulationSoftResetValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Soft Reset").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Hard Reset", priority = 137)]
    private static void EmulationHardReset()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Emulation/Hard Reset").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Emulation/Hard Reset", true, 137)]
    private static bool EmulationHardResetValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Emulation/Hard Reset").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Extract Layout", priority = 138)]
    private static void MFMEExtractLayout()
    {
        menubar.MenuItems.Single(item => item.FullPath == "MFME/Extract Layout").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Extract Layout", true, 138)]
    private static bool MFMEExtractLayoutValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "MFME/Extract Layout").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Remap MPU4 Lamps", priority = 139)]
    private static void MFMERemapMPU4Lamps()
    {
        menubar.MenuItems.Single(item => item.FullPath == "MFME/Remap MPU4 Lamps").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/MFME/Remap MPU4 Lamps", true, 139)]
    private static bool MFMERemapMPU4LampsValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "MFME/Remap MPU4 Lamps").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode On", priority = 140)]
    private static void ViewDisplayTextModeOn()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode On").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode On", true, 140)]
    private static bool ViewDisplayTextModeOnValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode On").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode Off", priority = 141)]
    private static void ViewDisplayTextModeOff()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode Off").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Display Text Mode Off", true, 141)]
    private static bool ViewDisplayTextModeOffValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Display Text Mode Off").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Add MAME View", priority = 152)]
    private static void ViewAddMAMEView()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Add MAME View").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Add MAME View", true, 152)]
    private static bool ViewAddMAMEViewValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Add MAME View").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Rebuild MAME View", priority = 153)]
    private static void ViewRebuildMAMEView()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Rebuild MAME View").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Rebuild MAME View", true, 153)]
    private static bool ViewRebuildMAMEViewValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Rebuild MAME View").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Show Base ViewQuads", priority = 164)]
    private static void ViewShowBaseViewQuads()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Show Base ViewQuads").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Show Base ViewQuads", true, 164)]
    private static bool ViewShowBaseViewQuadsValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Show Base ViewQuads").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Hide Base ViewQuads", priority = 165)]
    private static void ViewHideBaseViewQuads()
    {
        menubar.MenuItems.Single(item => item.FullPath == "View/Hide Base ViewQuads").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/View/Hide Base ViewQuads", true, 165)]
    private static bool ViewHideBaseViewQuadsValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "View/Hide Base ViewQuads").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Background/Upscale", priority = 166)]
    private static void BackgroundUpscale()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Background/Upscale").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Background/Upscale", true, 166)]
    private static bool BackgroundUpscaleValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Background/Upscale").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Background/Local Normalise", priority = 167)]
    private static void BackgroundLocalNormalise()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Background/Local Normalise").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Background/Local Normalise", true, 167)]
    private static bool BackgroundLocalNormaliseValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Background/Local Normalise").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Background/Global Normalise", priority = 168)]
    private static void BackgroundGlobalNormalise()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Background/Global Normalise").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Background/Global Normalise", true, 168)]
    private static bool BackgroundGlobalNormaliseValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Background/Global Normalise").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Window/Project", priority = 169)]
    private static void WindowProject()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Window/Project").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Window/Project", true, 169)]
    private static bool WindowProjectValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Window/Project").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Context Help", priority = 170)]
    private static void HelpContextHelp()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/Context Help").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Context Help", true, 170)]
    private static bool HelpContextHelpValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/Context Help").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Manual", priority = 171)]
    private static void HelpManual()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/Manual").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Manual", true, 171)]
    private static bool HelpManualValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/Manual").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Check For Updates", priority = 182)]
    private static void HelpCheckForUpdates()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/Check For Updates").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/Check For Updates", true, 182)]
    private static bool HelpCheckForUpdatesValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/Check For Updates").IsInteractable;
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/About", priority = 193)]
    private static void HelpAbout()
    {
        menubar.MenuItems.Single(item => item.FullPath == "Help/About").Action.Invoke();
    }
    [UnityEditor.MenuItem("NativeMenuBar/Help/About", true, 193)]
    private static bool HelpAboutValidate()
    {
        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == "Help/About").IsInteractable;
    }

}
