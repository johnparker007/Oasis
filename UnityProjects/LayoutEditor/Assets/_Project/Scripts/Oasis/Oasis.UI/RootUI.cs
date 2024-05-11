using Oasis.UI.ViewModels;

namespace Oasis.UI
{
    public class RootUI //: Form
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
            ViewModelMenu = new ViewModelMenu(this);
            //ViewModelStatusBar = new ViewModelStatusBar(this, this);
            //ViewModelHierarchy = new ViewModelHierarchy(this, this);
            //ViewModelInspector = new ViewModelInspector(this, this);
        }

        private void InitialiseForm()
        {
            //uwfHeaderHeight = 0;
            //uwfShadowBox = false;

            //// TODO need to figure this size stuff out once the 'rebuild ui on window resize'
            //// stuff is underway - user may have their taskbar to the side for instance.

            //Size = new Size(Screen.width, Screen.height);

            //MaximizeBox = false;
            //ControlBox = false;
            //AutoScroll = true;
            //BackColor = Color.Transparent;

            //uwfBorderColor = BackColor;

            //SetWindowState(FormWindowState.Maximized);
        }

        private void AddListeners()
        {
            ViewModelMenu.OnFileImportClick.AddListener(UIController.LayoutEditor.OnFileImportClick);
            ViewModelMenu.OnFileExportClick.AddListener(UIController.LayoutEditor.OnFileExportClick);

            ViewModelMenu.OnEmulationStartClick.AddListener(UIController.LayoutEditor.OnEmulationStartClick);
            ViewModelMenu.OnEmulationExitClick.AddListener(UIController.LayoutEditor.OnEmulationExitClick);
            ViewModelMenu.OnEmulationPauseClick.AddListener(UIController.LayoutEditor.OnEmulationPauseClick);
            ViewModelMenu.OnEmulationResumeClick.AddListener(UIController.LayoutEditor.OnEmulationResumeClick);
            ViewModelMenu.OnEmulationSoftResetClick.AddListener(UIController.LayoutEditor.OnEmulationSoftResetClick);
            ViewModelMenu.OnEmulationHardResetClick.AddListener(UIController.LayoutEditor.OnEmulationSoftResetClick);
            ViewModelMenu.OnEmulationThrottledClick.AddListener(UIController.LayoutEditor.OnEmulationThrottledClick);
            ViewModelMenu.OnEmulationUnthrottledClick.AddListener(UIController.LayoutEditor.OnEmulationUnthrottledClick);
            ViewModelMenu.OnEmulationStateLoadClick.AddListener(UIController.LayoutEditor.OnEmulationStateLoadClick);
            ViewModelMenu.OnEmulationStateSaveClick.AddListener(UIController.LayoutEditor.OnEmulationStateSaveClick);
            ViewModelMenu.OnEmulationStateSaveAndExitClick.AddListener(UIController.LayoutEditor.OnEmulationStateSaveAndExitClick);
            ViewModelMenu.OnEmulationStartAndStateLoadClick.AddListener(UIController.LayoutEditor.OnEmulationStartAndStateLoadClick);

            ViewModelMenu.OnMfmeExtractClick.AddListener(UIController.LayoutEditor.OnMfmeExtractClick);
            ViewModelMenu.OnMfmeRemapMpu4LampsClick.AddListener(UIController.LayoutEditor.OnMfmeRemapLampsClick);

            ViewModelMenu.OnHelpAboutClick.AddListener(UIController.LayoutEditor.OnHelpAboutClick);
            


        }
    }
}
