using Oasis.UI.Views;
using System.Windows.Forms;

namespace Oasis.UI.ViewModels
{
    public class ViewModelInspector : ViewModel
    {
        public ViewModelInspector(RootUI rootUI, Control parent) : base(rootUI, parent)
        {
            _view = new ViewInspector(rootUI, parent, this);
        }

        public override void Update()
        {
            base.Update();

            // just rebuild from scratch again for now:
            _view = new ViewInspector(_rootUI, _parent, this);
        }
    }
}
