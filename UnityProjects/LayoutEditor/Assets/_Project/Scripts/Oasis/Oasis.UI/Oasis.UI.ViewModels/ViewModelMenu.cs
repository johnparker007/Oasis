using System;
using UnityEngine.Events;

namespace Oasis.UI.ViewModels
{
    public class ViewModelMenu : ViewModel
    {
        public UnityEvent OnFileImportClick = new UnityEvent();
        public UnityEvent OnFileExportClick = new UnityEvent();

        public UnityEvent OnEmulationStartClick = new UnityEvent();
        public UnityEvent OnEmulationExitClick = new UnityEvent();
        public UnityEvent OnEmulationPauseClick = new UnityEvent();
        public UnityEvent OnEmulationResumeClick = new UnityEvent();
        public UnityEvent OnEmulationSoftResetClick = new UnityEvent();
        public UnityEvent OnEmulationHardResetClick = new UnityEvent();
        public UnityEvent OnEmulationThrottledClick = new UnityEvent();
        public UnityEvent OnEmulationUnthrottledClick = new UnityEvent();
        public UnityEvent OnEmulationStateLoadClick = new UnityEvent();
        public UnityEvent OnEmulationStateSaveClick = new UnityEvent();
        public UnityEvent OnEmulationStateSaveAndExitClick = new UnityEvent();
        public UnityEvent OnEmulationStartAndStateLoadClick = new UnityEvent();

        public UnityEvent OnMfmeExtractClick = new UnityEvent();
        public UnityEvent OnMfmeRemapMpu4LampsClick = new UnityEvent();

        public UnityEvent OnHelpAboutClick = new UnityEvent();


        //public ViewMenu ViewMenu
        //{
        //    get
        //    {
        //        return (ViewMenu)_view;
        //    }
        //}

        //public Size MenuStripSize
        //{
        //    get
        //    {
        //        return ViewMenu.MenuStrip.Size;
        //    }
        //}

        public ViewModelMenu(RootUI rootUI) : base(rootUI)
        {
            
        }

        public void OnFile_ImportClick(object sender, EventArgs e)
        {
            OnFileImportClick?.Invoke();
        }

        public void OnFile_ExportClick(object sender, EventArgs e)
        {
            OnFileExportClick?.Invoke();
        }

        public void OnEmulation_StartClick(object sender, EventArgs e)
        {
            OnEmulationStartClick?.Invoke();
        }

        public void OnEmulation_ExitClick(object sender, EventArgs e)
        {
            OnEmulationExitClick?.Invoke();
        }

        public void OnEmulation_PauseClick(object sender, EventArgs e)
        {
            OnEmulationPauseClick?.Invoke();
        }

        public void OnEmulation_ResumeClick(object sender, EventArgs e)
        {
            OnEmulationResumeClick?.Invoke();
        }

        public void OnEmulation_SoftResetClick(object sender, EventArgs e)
        {
            OnEmulationSoftResetClick?.Invoke();
        }

        public void OnEmulation_HardResetClick(object sender, EventArgs e)
        {
            OnEmulationHardResetClick?.Invoke();
        }

        public void OnEmulation_ThrottledClick(object sender, EventArgs e)
        {
            OnEmulationThrottledClick?.Invoke();
        }

        public void OnEmulation_UnthrottledClick(object sender, EventArgs e)
        {
            OnEmulationUnthrottledClick?.Invoke();
        }

        public void OnEmulation_StateLoadClick(object sender, EventArgs e)
        {
            OnEmulationStateLoadClick?.Invoke();
        }

        public void OnEmulation_StateSaveClick(object sender, EventArgs e)
        {
            OnEmulationStateSaveClick?.Invoke();
        }

        public void OnEmulation_StateSaveAndExitClick(object sender, EventArgs e)
        {
            OnEmulationStateSaveAndExitClick?.Invoke();
        }

        public void OnEmulation_StartAndStateLoadClick(object sender, EventArgs e)
        {
            OnEmulationStartAndStateLoadClick?.Invoke();
        }

        public void OnMfme_ExtractClick(object sender, EventArgs e)
        {
            OnMfmeExtractClick?.Invoke();
        }

        public void OnMfme_RemapMpu4LampsClick(object sender, EventArgs e)
        {
            OnMfmeRemapMpu4LampsClick?.Invoke();
        }

        public void OnHelp_AboutClick(object sender, EventArgs e)
        {
            OnHelpAboutClick?.Invoke();
        }
    }
}
