using Oasis.UI.ViewModels;

namespace Oasis.UI
{
    public class MfmeExtractForm //: Form
    {
        public ViewModelMfmeExtract ViewModelMfmeExtract
        {
            get;
            private set;
        } = null;

        public UIController UIController
        {
            get;
            private set;
        } = null;


        public MfmeExtractForm(UIController uiController)
        {
            UIController = uiController;

            InitialiseWinFormsUI();
        }

        private void InitialiseWinFormsUI()
        {
            InitialiseForm();
           // ViewModelMfmeExtract = new ViewModelMfmeExtract(UIController.RootUI, this);
        }

        private void InitialiseForm()
        {
            //uwfHeaderHeight = 0;
            //uwfShadowBox = false;

            // TODO need to figure this size stuff out once the 'rebuild ui on window resize'
            //// stuff is underway - user may have their taskbar to the side for instance.

            //Text = "MFME Extract";
            //FormBorderStyle = FormBorderStyle.FixedSingle;
            //MaximizeBox = false;
            //MinimumSize = new Size(480, 320);
            //Size = MinimumSize;
            //SizeGripStyle = SizeGripStyle.Hide;
            //StartPosition = FormStartPosition.CenterScreen;
            //TopMost = true;
            

            //MaximizeBox = false;
            //ControlBox = false;
            //AutoScroll = true;
            //BackColor = Color.Transparent;

            //uwfBorderColor = BackColor;

            //SetWindowState(FormWindowState.Maximized);
        }


    }
}
