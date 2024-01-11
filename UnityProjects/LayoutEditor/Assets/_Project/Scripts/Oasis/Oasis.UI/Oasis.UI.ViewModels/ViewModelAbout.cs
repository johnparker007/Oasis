using Oasis.UI.Views;
using System;
using System.Drawing;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Events;
using Oasis.LayoutEditor;

namespace Oasis.UI.ViewModels
{
    public class ViewModelAbout : ViewModel
    {
        public ViewAbout ViewAbout
        {
            get
            {
                return (ViewAbout)_view;
            }
        }

        public Size PanelSize
        {
            get
            {
                return ViewAbout.Panel.Size;
            }
        }


        public ViewModelAbout(RootUI rootUI, Control parent) : base(rootUI, parent)
        {
            _view = new ViewAbout(rootUI, parent, this);
        }
    }
}
