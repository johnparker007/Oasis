using Oasis.UI.Views;
using System.Drawing;
using System.Windows.Forms;

namespace Oasis.UI.ViewModels
{
    public class ViewModelMenu : ViewModel
    {
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
    }
}
