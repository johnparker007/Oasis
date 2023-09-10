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

        public ViewModelInspector ViewModelInspector
        {
            get;
            private set;
        } = null;

        public ViewModelStatusBar ViewModelStatusBar
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

            InitialiseWinFormsUI();

            AddListeners();
        }

        private void InitialiseWinFormsUI()
        {
            InitialiseForm();
            ViewModelMenu = new ViewModelMenu(this, this);
            ViewModelStatusBar = new ViewModelStatusBar(this, this);
            ViewModelHierarchy = new ViewModelHierarchy(this, this);
            ViewModelInspector = new ViewModelInspector(this, this);
        }

        private void InitialiseForm()
        {
            uwfHeaderHeight = 0;
            uwfShadowBox = false;

            // TODO need to figure this size stuff out once the 'rebuild ui on window resize'
            // stuff is underway - user may have their taskbar to the side for instance.

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

            ViewModelMenu.OnEmulationStartClick.AddListener(UIController.LayoutEditor.OnEmulationStartClick);
            ViewModelMenu.OnEmulationStopClick.AddListener(UIController.LayoutEditor.OnEmulationStopClick);
            ViewModelMenu.OnEmulationPauseClick.AddListener(UIController.LayoutEditor.OnEmulationPauseClick);
            ViewModelMenu.OnEmulationResetClick.AddListener(UIController.LayoutEditor.OnEmulationResetClick);
        }
    }
}
