using Oasis.UI.Views;
using System;
using System.Drawing;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Events;
using Oasis.LayoutEditor;

namespace Oasis.UI.ViewModels
{
    public class ViewModelMfmeExtract : ViewModel
    {
        public ViewMfmeExtract ViewMfmeExtract
        {
            get
            {
                return (ViewMfmeExtract)_view;
            }
        }

        public Size PanelSize
        {
            get
            {
                return ViewMfmeExtract.Panel.Size;
            }
        }


        public ViewModelMfmeExtract(RootUI rootUI, Control parent) : base(rootUI, parent)
        {
            _view = new ViewMfmeExtract(rootUI, parent, this);
        }
    }
}
