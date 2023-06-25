using Oasis.UI.ViewModels;
using System.Windows.Forms;

namespace Oasis.UI.Views
{
    public abstract class ViewBase
    {
        protected RootUI _rootUI = null;
        protected Control _parent = null;
        protected ViewModel _viewModel = null;

        public ViewBase(RootUI rootUI, Control parent, ViewModel viewModel)
        {
            _rootUI = rootUI;
            _parent = parent;
            _viewModel = viewModel;
        }
    }
}
