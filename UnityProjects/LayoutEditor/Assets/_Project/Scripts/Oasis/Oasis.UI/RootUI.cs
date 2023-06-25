using Oasis.UI.ViewModels;
using System.Drawing;
using System.Windows.Forms;

namespace Oasis.UI
{
    public class RootUI : Form
    {
        public ViewModelMenu ViewModelMenu
        {
            get;
            private set;
        } = null;

        public ViewModelHierarchy ViewModelHierarchy
        {
            get;
            private set;
        } = null;

        public UIController UIController
        {
            get;
            private set;
        } = null;


        public RootUI(UIController uiController)
        {
            UIController = uiController; 

            InitialiseForm();

            ViewModelMenu = new ViewModelMenu(this, this);
            ViewModelHierarchy = new ViewModelHierarchy(this, this);

            AddListeners();
        }

        private void InitialiseForm()
        {
            uwfHeaderHeight = 0;
            uwfShadowBox = false;

            Size = new Size(Screen.width, Screen.height);
            MaximizeBox = false;
            ControlBox = false;
            AutoScroll = true;
            BackColor = Color.Transparent;

            uwfBorderColor = BackColor;

            SetWindowState(FormWindowState.Maximized);
        }

        private void AddListeners()
        {
            ViewModelMenu.OnFileImportClick.AddListener(UIController.LayoutEditor.OnFileImportClick);
        }
    }
}
