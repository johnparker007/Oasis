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
    }
}
