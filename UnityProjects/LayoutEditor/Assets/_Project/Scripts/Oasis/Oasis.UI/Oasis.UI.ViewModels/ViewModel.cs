using Oasis.UI.Views;
using System.Windows.Forms;

namespace Oasis.UI.ViewModels
{
    public abstract class ViewModel 
    {
        protected RootUI _rootUI = null;
        protected Control _parent = null;
        protected ViewBase _view = null;

        public ViewModel(RootUI rootUI, Control parent)
        {
            _rootUI = rootUI;
            _parent = parent;
        }

        public virtual void Update()
        {

        }
    }
}
