namespace Oasis.UI.Views
{
    public abstract class ViewTab : ViewBase
    {
        //public abstract int ControlWidth
        //{
        //    get;
        //}

        //public abstract bool LeftAlignedPosition
        //{
        //    get;
        //}

        //public int LocationX
        //{
        //    get
        //    {
        //        if(LeftAlignedPosition)
        //        {
        //            return 0;
        //        }
        //        else
        //        {
        //            return _parent.Width - ControlWidth;
        //        }
        //    }
        //}

        //public abstract string TabName
        //{
        //    get;
        //}


        //protected TabControl _tabControl = null;
        //protected TabPage _tabPage = null;


        //public ViewTab(RootUI rootUI, Control parent, ViewModel viewModel) : base(rootUI, parent, viewModel)
        //{
        //    BuildTabControl();
        //    BuildTabPage();

        //    _tabControl.TabPages.Add(_tabPage);

        //    _parent.Controls.Add(_tabControl);
        //}

        //private void BuildTabControl()
        //{
        //    //_tabControl = new TabControl();

        //    //_tabControl.Location = new Point(LocationX, _rootUI.uwfHeaderHeight - 1 + _rootUI.ViewModelMenu.MenuStripSize.Height);
        //    ////_treeViewPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
        //    //_tabControl.Height = _parent.Height
        //    //    - _rootUI.uwfHeaderHeight
        //    //    + 1
        //    //    - _rootUI.ViewModelMenu.MenuStripSize.Height
        //    //    - _rootUI.ViewModelStatusBar.ViewStatusBar.Panel.Height;
            
        //    //_tabControl.Width = ControlWidth;
        //    ////_treeView.TabStop = false;
        //    ////_treeView.Width = 220;
        //    //_tabControl.Padding = new Padding(0);

        //    //_tabControl.BackColor = Color.FromArgb(40, 40, 40); // JP Dark theme
        //}

        //private void BuildTabPage()
        //{
        //    _tabPage = new TabPage();
        //    _tabPage.Size = _tabControl.Size;
        //    _tabPage.Text = TabName;
        //}
    }
}
