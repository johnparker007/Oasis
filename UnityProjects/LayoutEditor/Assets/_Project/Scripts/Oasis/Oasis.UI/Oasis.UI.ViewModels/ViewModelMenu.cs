using Oasis.UI.Views;
using System;
using System.Drawing;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.UI.ViewModels
{
    public class ViewModelMenu : ViewModel
    {
        public UnityEvent OnFileImportClick = new UnityEvent();

        public UnityEvent OnEmulationStartClick = new UnityEvent();
        public UnityEvent OnEmulationStopClick = new UnityEvent();
        public UnityEvent OnEmulationPauseClick = new UnityEvent();
        public UnityEvent OnEmulationResetClick = new UnityEvent();

        public ViewMenu ViewMenu
        {
            get
            {
                return (ViewMenu)_view;
            }
        }

        public Size MenuStripSize
        {
            get
            {
                return ViewMenu.MenuStrip.Size;
            }
        }

        public ViewModelMenu(RootUI rootUI, Control parent) : base(rootUI, parent)
        {
            _view = new ViewMenu(rootUI, parent, this);
        }

        public void OnFile_ImportClick(object sender, EventArgs e)
        {
            OnFileImportClick?.Invoke();
        }

        public void OnEmulation_StartClick(object sender, EventArgs e)
        {
            OnEmulationStartClick?.Invoke();
        }

        public void OnEmulation_StopClick(object sender, EventArgs e)
        {
            OnEmulationStopClick?.Invoke();
        }

        public void OnEmulation_PauseClick(object sender, EventArgs e)
        {
            OnEmulationPauseClick?.Invoke();
        }

        public void OnEmulation_ResetClick(object sender, EventArgs e)
        {
            OnEmulationResetClick?.Invoke();
        }
    }
}
