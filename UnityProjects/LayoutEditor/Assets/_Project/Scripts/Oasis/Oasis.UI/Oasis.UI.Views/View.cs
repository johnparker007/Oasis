using Oasis.UI.ViewModels;
using System.Windows.Forms;

namespace Oasis.UI.Views
{
    public class View
    {
        protected RootUI _rootUI = null;
        protected Control _parent = null;
        protected ViewModel _viewModel = null;

        public View(RootUI rootUI, Control parent, ViewModel viewModel)
        {
            _rootUI = rootUI;
            _parent = parent;
            _viewModel = viewModel;
        }
    }
}
