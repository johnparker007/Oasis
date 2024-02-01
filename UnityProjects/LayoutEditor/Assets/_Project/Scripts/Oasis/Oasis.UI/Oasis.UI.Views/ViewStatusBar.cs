namespace Oasis.UI.Views
{
    public class ViewStatusBar : ViewBase
    {
        //public Panel Panel
        //{
        //    get;
        //    private set;
        //} = null;

        //public Label ZoomLabel
        //{ 
        //    get;
        //    private set;
        //} = null;


        //protected ViewModelStatusBar ViewModelStatusBar
        //{
        //    get
        //    {
        //        return (ViewModelStatusBar)_viewModel;
        //    }
        //}

        //public ViewStatusBar(RootUI rootUI, Control parent, ViewModel viewModel) : base(rootUI, parent, viewModel)
        //{
        //    BuildStatusBarPanel();

        //    _parent.Controls.Add(Panel);

        //    ViewModelStatusBar.OnZoomChanged.AddListener(OnZoomChanged);
        //}

        //private void BuildStatusBarPanel()
        //{
        //    Panel = Activator.CreateInstance(typeof(Panel)) as Panel;

        //    Panel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;

        //    // Not sure where this should ideally be defined yet:
        //    const int kPanelHeight = 24;
        //    Panel.Height = kPanelHeight;
        //    Panel.Width = _parent.Width;
        //    Panel.BackColor = Color.FromArgb(56, 56, 56); // JP Dark theme

        //    Panel.Location = new Point(0, _parent.Height - Panel.Height);

        //    ZoomLabel = new Label();
        //    RefreshZoomLabel();

        //    Panel.Controls.Add(ZoomLabel);
        //}

        //private void RefreshZoomLabel()
        //{
        //    ZoomLabel.Text = string.Format(
        //        "Zoom: {0}%", 
        //        (ViewModelStatusBar.Zoom.ZoomLevel * 100f).ToString("0.0"));
        //}

        //private void OnZoomChanged()
        //{
        //    RefreshZoomLabel();
        //}
    }

}
