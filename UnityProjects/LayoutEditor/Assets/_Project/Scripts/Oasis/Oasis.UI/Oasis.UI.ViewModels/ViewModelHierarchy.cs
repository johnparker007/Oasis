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

        public override void Update()
        {
            base.Update();

            // just rebuild from scratch again for now:
            _view = new ViewHierarchy(_rootUI, _parent, this);
        }
    }
}
