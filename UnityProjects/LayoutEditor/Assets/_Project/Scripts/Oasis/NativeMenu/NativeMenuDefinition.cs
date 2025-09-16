using System;
using System.Collections.Generic;
using Oasis.NativeMenus;

namespace Oasis.NativeMenu
{
    internal static class NativeMenuDefinition
    {
        public static IReadOnlyList<MenuEntry> BuildDefaultMenu(SelectionHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return new List<MenuEntry>
            {
                Item("File/New Project", 0, handler.OnFileNewProject),
                Item("File/Open Project", 1, handler.OnFileOpenProject),
                Item("File/Save Project", 2, handler.OnFileSaveProject),
                Item("File/Import MFME", 13, handler.OnFileImportMfme),
                Item("File/Export MAME", 14, handler.OnFileExportMAME),
                Item("File/Close", 25, handler.OnFileClose),
                Item("File/Exit", 26, handler.OnFileExit),

                Item("Edit/Undo", 27, null, Disabled),
                Item("Edit/Redo", 28, null, Disabled),
                Item("Edit/Cut", 39, null, Disabled),
                Item("Edit/Copy", 40, null, Disabled),
                Item("Edit/Paste", 41, null, Disabled),
                Item("Edit/Duplicate", 52, null, Disabled),
                Item("Edit/Rename", 53, null, Disabled),
                Item("Edit/Delete", 54, null, Disabled),
                Item("Edit/Project Settings", 65, handler.OnEditProjectSettings),
                Item("Edit/Preferences", 66, handler.OnEditPreferences),

                Item("Component/Lamp", 67, null, Disabled),
                Item("Component/LED", 68, null, Disabled),
                Item("Component/Reel", 69, null, Disabled),
                Item("Component/Display", 70, null, Disabled),
                Item("Component/Button", 71, null, Disabled),
                Item("Component/Screen", 72, null, Disabled),
                Item("Component/Label", 73, null, Disabled),
                Item("Component/Image", 74, null, Disabled),
                Item("Component/Group", 85, null, Disabled),

                Item("Emulation/Start And Load State", 86, handler.OnEmulationStartAndStateLoad),
                Item("Emulation/Save State And Exit", 87, handler.OnEmulationStateSaveAndExit),
                Item("Emulation/Start", 98, handler.OnEmulationStart),
                Item("Emulation/Load State", 99, handler.OnEmulationStateLoad),
                Item("Emulation/Save State", 100, handler.OnEmulationStateSave),
                Item("Emulation/Exit", 101, handler.OnEmulationExit),
                Item("Emulation/Pause", 112, handler.OnEmulationPause),
                Item("Emulation/Resume", 113, handler.OnEmulationResume),
                Item("Emulation/Throttle", 124, handler.OnEmulationThrottled),
                Item("Emulation/Unthrottle", 125, handler.OnEmulationUnthrottled),
                Item("Emulation/Soft Reset", 136, handler.OnEmulationSoftReset),
                Item("Emulation/Hard Reset", 137, handler.OnEmulationHardReset),

                Item("MFME/Extract Layout", 138, handler.OnMfmeExtract),
                Item("MFME/Remap MPU4 Lamps", 139, handler.OnMfmeRemapLamps),

                Item("View/Display Text Mode On", 140, handler.OnViewDisplayTextOn),
                Item("View/Display Text Mode Off", 141, handler.OnViewDisplayTextOff),
                Item("View/Add MAME View", 152, handler.OnViewAddMameView),
                Item("View/Rebuild MAME View", 153, handler.OnViewRebuildMameView),
                Item("View/Show Base ViewQuads", 164, handler.OnViewShowBaseViewQuads),
                Item("View/Hide Base ViewQuads", 165, handler.OnViewHideBaseViewQuads),

                Item("Background/Upscale", 166, handler.OnBackgroundUpscale),
                Item("Background/Local Normalise", 167, handler.OnBackgroundLocalNormalise),
                Item("Background/Global Normalise", 168, handler.OnBackgroundGlobalNormalise),

                Item("Window/Hierarchy", 169, handler.OnWindowShowHierarchy, handler.CanShowWindowHierarchy),
                Item("Window/Inspector", 170, handler.OnWindowShowInspector, handler.CanShowWindowInspector),
                Item("Window/Project", 171, handler.OnWindowShowProject, handler.CanShowWindowProject),
                Item("Window/Base View", 172, handler.OnWindowShowBaseView, handler.CanShowWindowBaseView),

                Item("Help/Context Help", 170, null, Disabled),
                Item("Help/Manual", 171, null, Disabled),
                Item("Help/Check For Updates", 182, null, Disabled),
                Item("Help/About", 193, handler.OnHelpAbout),
            };
        }

        private static MenuEntry Item(string path, int priority, Action action, Func<bool> enabled = null, Func<bool> isChecked = null, string shortcut = null)
        {
            return new MenuEntry(path, priority, action, enabled, isChecked, shortcut);
        }

        private static bool Disabled() => false;
    }
}
