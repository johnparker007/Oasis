using Oasis.UI.Views;
using System.Windows.Forms;

namespace Oasis.UI.ViewModels
{
    public class ViewModelHierarchy : ViewModel
    {
        public ViewModelHierarchy(RootUI rootUI, Control parent) : base(rootUI, parent)
        {
            _view = new ViewHierarchy(rootUI, parent, this);
        }
    }
}
