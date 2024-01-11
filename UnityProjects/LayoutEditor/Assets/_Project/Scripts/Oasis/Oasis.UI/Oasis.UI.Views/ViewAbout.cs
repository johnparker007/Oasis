using Oasis.UI.ViewModels;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Oasis.UI.Views
{
    public class ViewAbout : ViewBase
    {
        public Panel Panel
        {
            get;
            private set;
        } = null;


        protected ViewModelAbout ViewModelAbout
        {
            get
            {
                return (ViewModelAbout)_viewModel;
            }
        }

        public ViewAbout(RootUI rootUI, Control parent, ViewModel viewModel) : base(rootUI, parent, viewModel)
        {
            BuildAboutPanel();

            _parent.Controls.Add(Panel);
        }

        private void BuildAboutPanel()
        {
            Panel = Activator.CreateInstance(typeof(Panel)) as Panel;

            Panel.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Not sure where this should ideally be defined yet:
            const int kPanelWidth = 480;
            const int kPanelHeight = 320;
            Panel.Height = kPanelHeight;
            Panel.Width = kPanelWidth;
            Panel.BackColor = Color.FromArgb(56, 56, 56); // JP Dark theme

            Panel.Location = new Point(0, _parent.Height - Panel.Height);



            Label label = new Label();
            label.Text = "test";
            label.Location = new Point(100, 100);
            Panel.Controls.Add(label);

            //ZoomLabel = new Label();
            //RefreshZoomLabel();

            //Panel.Controls.Add(ZoomLabel);
        }

        //private void RefreshZoomLabel()
        //{
        //    ZoomLabel.Text = string.Format(
        //        "Zoom: {0}%", 
        //        (ViewModelStatusBar.Zoom.ZoomLevel * 100f).ToString("0.0"));
        //}
    }

}
