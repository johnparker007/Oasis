#if UNITY_EDITOR
using UnityEditor;

namespace Oasis.NativeMenu.Editor
{
    internal static class NativeMenuEditorMenu
    {
        private const string MenuPrefix = "NativeMenu";

        private static void Execute(string relativePath)
        {
            NativeMenuRegistry.Execute(relativePath);
        }

        private static bool Validate(string relativePath)
        {
            return NativeMenuRegistry.IsItemEnabled(relativePath);
        }

        [MenuItem(MenuPrefix + "/File/New Project", priority = 0)]
        private static void FileNewProject() => Execute("File/New Project");
        [MenuItem(MenuPrefix + "/File/New Project", true, 0)]
        private static bool FileNewProjectValidate() => Validate("File/New Project");

        [MenuItem(MenuPrefix + "/File/Open Project", priority = 1)]
        private static void FileOpenProject() => Execute("File/Open Project");
        [MenuItem(MenuPrefix + "/File/Open Project", true, 1)]
        private static bool FileOpenProjectValidate() => Validate("File/Open Project");

        [MenuItem(MenuPrefix + "/File/Save Project", priority = 2)]
        private static void FileSaveProject() => Execute("File/Save Project");
        [MenuItem(MenuPrefix + "/File/Save Project", true, 2)]
        private static bool FileSaveProjectValidate() => Validate("File/Save Project");

        [MenuItem(MenuPrefix + "/File/Import MFME", priority = 13)]
        private static void FileImportMfme() => Execute("File/Import MFME");
        [MenuItem(MenuPrefix + "/File/Import MFME", true, 13)]
        private static bool FileImportMfmeValidate() => Validate("File/Import MFME");

        [MenuItem(MenuPrefix + "/File/Export MAME", priority = 14)]
        private static void FileExportMame() => Execute("File/Export MAME");
        [MenuItem(MenuPrefix + "/File/Export MAME", true, 14)]
        private static bool FileExportMameValidate() => Validate("File/Export MAME");

        [MenuItem(MenuPrefix + "/File/Close", priority = 25)]
        private static void FileClose() => Execute("File/Close");
        [MenuItem(MenuPrefix + "/File/Close", true, 25)]
        private static bool FileCloseValidate() => Validate("File/Close");

        [MenuItem(MenuPrefix + "/File/Exit", priority = 26)]
        private static void FileExit() => Execute("File/Exit");
        [MenuItem(MenuPrefix + "/File/Exit", true, 26)]
        private static bool FileExitValidate() => Validate("File/Exit");

        [MenuItem(MenuPrefix + "/Edit/Undo", priority = 27)]
        private static void EditUndo() => Execute("Edit/Undo");
        [MenuItem(MenuPrefix + "/Edit/Undo", true, 27)]
        private static bool EditUndoValidate() => Validate("Edit/Undo");

        [MenuItem(MenuPrefix + "/Edit/Redo", priority = 28)]
        private static void EditRedo() => Execute("Edit/Redo");
        [MenuItem(MenuPrefix + "/Edit/Redo", true, 28)]
        private static bool EditRedoValidate() => Validate("Edit/Redo");

        [MenuItem(MenuPrefix + "/Edit/Cut", priority = 39)]
        private static void EditCut() => Execute("Edit/Cut");
        [MenuItem(MenuPrefix + "/Edit/Cut", true, 39)]
        private static bool EditCutValidate() => Validate("Edit/Cut");

        [MenuItem(MenuPrefix + "/Edit/Copy", priority = 40)]
        private static void EditCopy() => Execute("Edit/Copy");
        [MenuItem(MenuPrefix + "/Edit/Copy", true, 40)]
        private static bool EditCopyValidate() => Validate("Edit/Copy");

        [MenuItem(MenuPrefix + "/Edit/Paste", priority = 41)]
        private static void EditPaste() => Execute("Edit/Paste");
        [MenuItem(MenuPrefix + "/Edit/Paste", true, 41)]
        private static bool EditPasteValidate() => Validate("Edit/Paste");

        [MenuItem(MenuPrefix + "/Edit/Duplicate", priority = 52)]
        private static void EditDuplicate() => Execute("Edit/Duplicate");
        [MenuItem(MenuPrefix + "/Edit/Duplicate", true, 52)]
        private static bool EditDuplicateValidate() => Validate("Edit/Duplicate");

        [MenuItem(MenuPrefix + "/Edit/Rename", priority = 53)]
        private static void EditRename() => Execute("Edit/Rename");
        [MenuItem(MenuPrefix + "/Edit/Rename", true, 53)]
        private static bool EditRenameValidate() => Validate("Edit/Rename");

        [MenuItem(MenuPrefix + "/Edit/Delete", priority = 54)]
        private static void EditDelete() => Execute("Edit/Delete");
        [MenuItem(MenuPrefix + "/Edit/Delete", true, 54)]
        private static bool EditDeleteValidate() => Validate("Edit/Delete");

        [MenuItem(MenuPrefix + "/Edit/Project Settings", priority = 65)]
        private static void EditProjectSettings() => Execute("Edit/Project Settings");
        [MenuItem(MenuPrefix + "/Edit/Project Settings", true, 65)]
        private static bool EditProjectSettingsValidate() => Validate("Edit/Project Settings");

        [MenuItem(MenuPrefix + "/Edit/Preferences", priority = 66)]
        private static void EditPreferences() => Execute("Edit/Preferences");
        [MenuItem(MenuPrefix + "/Edit/Preferences", true, 66)]
        private static bool EditPreferencesValidate() => Validate("Edit/Preferences");

        [MenuItem(MenuPrefix + "/Component/Lamp", priority = 67)]
        private static void ComponentLamp() => Execute("Component/Lamp");
        [MenuItem(MenuPrefix + "/Component/Lamp", true, 67)]
        private static bool ComponentLampValidate() => Validate("Component/Lamp");

        [MenuItem(MenuPrefix + "/Component/LED", priority = 68)]
        private static void ComponentLed() => Execute("Component/LED");
        [MenuItem(MenuPrefix + "/Component/LED", true, 68)]
        private static bool ComponentLedValidate() => Validate("Component/LED");

        [MenuItem(MenuPrefix + "/Component/Reel", priority = 69)]
        private static void ComponentReel() => Execute("Component/Reel");
        [MenuItem(MenuPrefix + "/Component/Reel", true, 69)]
        private static bool ComponentReelValidate() => Validate("Component/Reel");

        [MenuItem(MenuPrefix + "/Component/Display", priority = 70)]
        private static void ComponentDisplay() => Execute("Component/Display");
        [MenuItem(MenuPrefix + "/Component/Display", true, 70)]
        private static bool ComponentDisplayValidate() => Validate("Component/Display");

        [MenuItem(MenuPrefix + "/Component/Button", priority = 71)]
        private static void ComponentButton() => Execute("Component/Button");
        [MenuItem(MenuPrefix + "/Component/Button", true, 71)]
        private static bool ComponentButtonValidate() => Validate("Component/Button");

        [MenuItem(MenuPrefix + "/Component/Screen", priority = 72)]
        private static void ComponentScreen() => Execute("Component/Screen");
        [MenuItem(MenuPrefix + "/Component/Screen", true, 72)]
        private static bool ComponentScreenValidate() => Validate("Component/Screen");

        [MenuItem(MenuPrefix + "/Component/Label", priority = 73)]
        private static void ComponentLabel() => Execute("Component/Label");
        [MenuItem(MenuPrefix + "/Component/Label", true, 73)]
        private static bool ComponentLabelValidate() => Validate("Component/Label");

        [MenuItem(MenuPrefix + "/Component/Image", priority = 74)]
        private static void ComponentImage() => Execute("Component/Image");
        [MenuItem(MenuPrefix + "/Component/Image", true, 74)]
        private static bool ComponentImageValidate() => Validate("Component/Image");

        [MenuItem(MenuPrefix + "/Component/Group", priority = 85)]
        private static void ComponentGroup() => Execute("Component/Group");
        [MenuItem(MenuPrefix + "/Component/Group", true, 85)]
        private static bool ComponentGroupValidate() => Validate("Component/Group");

        [MenuItem(MenuPrefix + "/Emulation/Start And Load State", priority = 86)]
        private static void EmulationStartAndLoadState() => Execute("Emulation/Start And Load State");
        [MenuItem(MenuPrefix + "/Emulation/Start And Load State", true, 86)]
        private static bool EmulationStartAndLoadStateValidate() => Validate("Emulation/Start And Load State");

        [MenuItem(MenuPrefix + "/Emulation/Save State And Exit", priority = 87)]
        private static void EmulationSaveStateAndExit() => Execute("Emulation/Save State And Exit");
        [MenuItem(MenuPrefix + "/Emulation/Save State And Exit", true, 87)]
        private static bool EmulationSaveStateAndExitValidate() => Validate("Emulation/Save State And Exit");

        [MenuItem(MenuPrefix + "/Emulation/Start", priority = 98)]
        private static void EmulationStart() => Execute("Emulation/Start");
        [MenuItem(MenuPrefix + "/Emulation/Start", true, 98)]
        private static bool EmulationStartValidate() => Validate("Emulation/Start");

        [MenuItem(MenuPrefix + "/Emulation/Load State", priority = 99)]
        private static void EmulationLoadState() => Execute("Emulation/Load State");
        [MenuItem(MenuPrefix + "/Emulation/Load State", true, 99)]
        private static bool EmulationLoadStateValidate() => Validate("Emulation/Load State");

        [MenuItem(MenuPrefix + "/Emulation/Save State", priority = 100)]
        private static void EmulationSaveState() => Execute("Emulation/Save State");
        [MenuItem(MenuPrefix + "/Emulation/Save State", true, 100)]
        private static bool EmulationSaveStateValidate() => Validate("Emulation/Save State");

        [MenuItem(MenuPrefix + "/Emulation/Exit", priority = 101)]
        private static void EmulationExit() => Execute("Emulation/Exit");
        [MenuItem(MenuPrefix + "/Emulation/Exit", true, 101)]
        private static bool EmulationExitValidate() => Validate("Emulation/Exit");

        [MenuItem(MenuPrefix + "/Emulation/Pause", priority = 112)]
        private static void EmulationPause() => Execute("Emulation/Pause");
        [MenuItem(MenuPrefix + "/Emulation/Pause", true, 112)]
        private static bool EmulationPauseValidate() => Validate("Emulation/Pause");

        [MenuItem(MenuPrefix + "/Emulation/Resume", priority = 113)]
        private static void EmulationResume() => Execute("Emulation/Resume");
        [MenuItem(MenuPrefix + "/Emulation/Resume", true, 113)]
        private static bool EmulationResumeValidate() => Validate("Emulation/Resume");

        [MenuItem(MenuPrefix + "/Emulation/Throttle", priority = 124)]
        private static void EmulationThrottle() => Execute("Emulation/Throttle");
        [MenuItem(MenuPrefix + "/Emulation/Throttle", true, 124)]
        private static bool EmulationThrottleValidate() => Validate("Emulation/Throttle");

        [MenuItem(MenuPrefix + "/Emulation/Unthrottle", priority = 125)]
        private static void EmulationUnthrottle() => Execute("Emulation/Unthrottle");
        [MenuItem(MenuPrefix + "/Emulation/Unthrottle", true, 125)]
        private static bool EmulationUnthrottleValidate() => Validate("Emulation/Unthrottle");

        [MenuItem(MenuPrefix + "/Emulation/Soft Reset", priority = 136)]
        private static void EmulationSoftReset() => Execute("Emulation/Soft Reset");
        [MenuItem(MenuPrefix + "/Emulation/Soft Reset", true, 136)]
        private static bool EmulationSoftResetValidate() => Validate("Emulation/Soft Reset");

        [MenuItem(MenuPrefix + "/Emulation/Hard Reset", priority = 137)]
        private static void EmulationHardReset() => Execute("Emulation/Hard Reset");
        [MenuItem(MenuPrefix + "/Emulation/Hard Reset", true, 137)]
        private static bool EmulationHardResetValidate() => Validate("Emulation/Hard Reset");

        [MenuItem(MenuPrefix + "/MFME/Extract Layout", priority = 138)]
        private static void MfmeExtractLayout() => Execute("MFME/Extract Layout");
        [MenuItem(MenuPrefix + "/MFME/Extract Layout", true, 138)]
        private static bool MfmeExtractLayoutValidate() => Validate("MFME/Extract Layout");

        [MenuItem(MenuPrefix + "/MFME/Remap MPU4 Lamps", priority = 139)]
        private static void MfmeRemapMpu4Lamps() => Execute("MFME/Remap MPU4 Lamps");
        [MenuItem(MenuPrefix + "/MFME/Remap MPU4 Lamps", true, 139)]
        private static bool MfmeRemapMpu4LampsValidate() => Validate("MFME/Remap MPU4 Lamps");

        [MenuItem(MenuPrefix + "/View/Display Text Mode On", priority = 140)]
        private static void ViewDisplayTextModeOn() => Execute("View/Display Text Mode On");
        [MenuItem(MenuPrefix + "/View/Display Text Mode On", true, 140)]
        private static bool ViewDisplayTextModeOnValidate() => Validate("View/Display Text Mode On");

        [MenuItem(MenuPrefix + "/View/Display Text Mode Off", priority = 141)]
        private static void ViewDisplayTextModeOff() => Execute("View/Display Text Mode Off");
        [MenuItem(MenuPrefix + "/View/Display Text Mode Off", true, 141)]
        private static bool ViewDisplayTextModeOffValidate() => Validate("View/Display Text Mode Off");

        [MenuItem(MenuPrefix + "/View/Add ViewQuad", priority = 150)]
        private static void ViewAddViewQuad() => Execute("View/Add ViewQuad");
        [MenuItem(MenuPrefix + "/View/Add ViewQuad", true, 150)]
        private static bool ViewAddViewQuadValidate() => Validate("View/Add ViewQuad");

        [MenuItem(MenuPrefix + "/View/Add MAME View", priority = 152)]
        private static void ViewAddMameView() => Execute("View/Add MAME View");
        [MenuItem(MenuPrefix + "/View/Add MAME View", true, 152)]
        private static bool ViewAddMameViewValidate() => Validate("View/Add MAME View");

        [MenuItem(MenuPrefix + "/View/Rebuild MAME View", priority = 153)]
        private static void ViewRebuildMameView() => Execute("View/Rebuild MAME View");
        [MenuItem(MenuPrefix + "/View/Rebuild MAME View", true, 153)]
        private static bool ViewRebuildMameViewValidate() => Validate("View/Rebuild MAME View");

        [MenuItem(MenuPrefix + "/Background/Upscale", priority = 166)]
        private static void BackgroundUpscale() => Execute("Background/Upscale");
        [MenuItem(MenuPrefix + "/Background/Upscale", true, 166)]
        private static bool BackgroundUpscaleValidate() => Validate("Background/Upscale");

        [MenuItem(MenuPrefix + "/Background/Local Normalise", priority = 167)]
        private static void BackgroundLocalNormalise() => Execute("Background/Local Normalise");
        [MenuItem(MenuPrefix + "/Background/Local Normalise", true, 167)]
        private static bool BackgroundLocalNormaliseValidate() => Validate("Background/Local Normalise");

        [MenuItem(MenuPrefix + "/Background/Global Normalise", priority = 168)]
        private static void BackgroundGlobalNormalise() => Execute("Background/Global Normalise");
        [MenuItem(MenuPrefix + "/Background/Global Normalise", true, 168)]
        private static bool BackgroundGlobalNormaliseValidate() => Validate("Background/Global Normalise");

        [MenuItem(MenuPrefix + "/Window/Hierarchy", priority = 169)]
        private static void WindowHierarchy() => Execute("Window/Hierarchy");
        [MenuItem(MenuPrefix + "/Window/Hierarchy", true, 169)]
        private static bool WindowHierarchyValidate() => Validate("Window/Hierarchy");

        [MenuItem(MenuPrefix + "/Window/Inspector", priority = 170)]
        private static void WindowInspector() => Execute("Window/Inspector");
        [MenuItem(MenuPrefix + "/Window/Inspector", true, 170)]
        private static bool WindowInspectorValidate() => Validate("Window/Inspector");

        [MenuItem(MenuPrefix + "/Window/Project", priority = 171)]
        private static void WindowProject() => Execute("Window/Project");
        [MenuItem(MenuPrefix + "/Window/Project", true, 171)]
        private static bool WindowProjectValidate() => Validate("Window/Project");

        [MenuItem(MenuPrefix + "/Window/Base View", priority = 172)]
        private static void WindowBaseView() => Execute("Window/Base View");
        [MenuItem(MenuPrefix + "/Window/Base View", true, 172)]
        private static bool WindowBaseViewValidate() => Validate("Window/Base View");

        [MenuItem(MenuPrefix + "/Help/Context Help", priority = 170)]
        private static void HelpContextHelp() => Execute("Help/Context Help");
        [MenuItem(MenuPrefix + "/Help/Context Help", true, 170)]
        private static bool HelpContextHelpValidate() => Validate("Help/Context Help");

        [MenuItem(MenuPrefix + "/Help/Manual", priority = 171)]
        private static void HelpManual() => Execute("Help/Manual");
        [MenuItem(MenuPrefix + "/Help/Manual", true, 171)]
        private static bool HelpManualValidate() => Validate("Help/Manual");

        [MenuItem(MenuPrefix + "/Help/Check For Updates", priority = 182)]
        private static void HelpCheckForUpdates() => Execute("Help/Check For Updates");
        [MenuItem(MenuPrefix + "/Help/Check For Updates", true, 182)]
        private static bool HelpCheckForUpdatesValidate() => Validate("Help/Check For Updates");

        [MenuItem(MenuPrefix + "/Help/About", priority = 193)]
        private static void HelpAbout() => Execute("Help/About");
        [MenuItem(MenuPrefix + "/Help/About", true, 193)]
        private static bool HelpAboutValidate() => Validate("Help/About");

    }
}
#endif
