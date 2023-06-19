namespace Oasis.UI
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;
    using UnityWinForms.Examples;

    public class UI : Form
    {
        // for now, hard code some values:
        private const int kTreeViewWidth = 220;

        private Panel _menuPanel = null;
        private MenuStrip _menuStrip = null;

        private Panel _treeViewPanel = null;
        private TreeView _treeView = null;
        private TabControl _tabControl = null;
        private TabPage _tabPage = null;

        public UI()
        {
            InitialiseForm();
            BuildMenu();

            BuildHierarchy();
            Build2D();
            BuildInspector();
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

        private void BuildMenu()
        {
            _menuStrip = new MenuStrip();
            BuildMenuPanel();

            _menuStrip.Items.Add(BuildMenuFile());
            _menuStrip.Items.Add(BuildMenuEdit());
            _menuStrip.Items.Add(BuildMenuComponent());
            _menuStrip.Items.Add(BuildMenuHelp());

            _menuPanel.Controls.Add(_menuStrip);

            Controls.Add(_menuPanel);

            // for ref:
            ////itemFile_New.ShortcutKeys = Keys.Control | Keys.N;
            ////itemFile_Save.ShortcutKeys = Keys.Control | Keys.S;
            ////itemFile_Exit.ShortcutKeys = Keys.Control | Keys.W;

            //itemFile_Exit.Image = uwfAppOwner.Resources.Close;
            //itemFile_Exit.uwfImageColor = Color.FromArgb(64, 64, 64);
        }

        private void BuildMenuPanel()
        {
            _menuPanel = Activator.CreateInstance(typeof(Panel)) as Panel;

            _menuPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _menuPanel.Height = _menuStrip.Height;
            _menuPanel.Width = Width;
        }

        private ToolStripMenuItem BuildMenuFile()
        {
            var itemFile = new ToolStripMenuItem("File");

            var itemFile_New = new ToolStripMenuItem("New...");
            var itemFile_Open = new ToolStripMenuItem("Open...");
            var itemFile_Save = new ToolStripMenuItem("Save...");
            var itemFile_SaveAs = new ToolStripMenuItem("Save As...");

            var itemFile_Import = new ToolStripMenuItem("Import");
            var itemFile_Export = new ToolStripMenuItem("Export");

            var itemFile_Close = new ToolStripMenuItem("Close");
            var itemFile_Exit = new ToolStripMenuItem("Exit");

            itemFile.DropDownItems.Add(itemFile_New);
            itemFile.DropDownItems.Add(itemFile_Open);
            itemFile.DropDownItems.Add(itemFile_Save);
            itemFile.DropDownItems.Add(itemFile_SaveAs);
            itemFile.DropDownItems.Add(new ToolStripSeparator());
            itemFile.DropDownItems.Add(itemFile_Import);
            itemFile.DropDownItems.Add(itemFile_Export);
            itemFile.DropDownItems.Add(new ToolStripSeparator());
            itemFile.DropDownItems.Add(itemFile_Close);
            itemFile.DropDownItems.Add(itemFile_Exit);

            return itemFile;
        }

        private ToolStripMenuItem BuildMenuEdit()
        {
            var itemEdit = new ToolStripMenuItem("Edit");

            var itemEdit_Undo = new ToolStripMenuItem("Undo");
            var itemEdit_Redo = new ToolStripMenuItem("Redo");

            var itemEdit_Cut = new ToolStripMenuItem("Cut");
            var itemEdit_Copy = new ToolStripMenuItem("Copy");
            var itemEdit_Paste = new ToolStripMenuItem("Paste");

            var itemEdit_Duplicate = new ToolStripMenuItem("Duplicate");
            var itemEdit_Rename = new ToolStripMenuItem("Rename");
            var itemEdit_Delete = new ToolStripMenuItem("Delete");

            var itemEdit_Preferences = new ToolStripMenuItem("Preferences");

            itemEdit.DropDownItems.Add(itemEdit_Undo);
            itemEdit.DropDownItems.Add(itemEdit_Redo);
            itemEdit.DropDownItems.Add(new ToolStripSeparator());
            itemEdit.DropDownItems.Add(itemEdit_Cut);
            itemEdit.DropDownItems.Add(itemEdit_Copy);
            itemEdit.DropDownItems.Add(itemEdit_Paste);
            itemEdit.DropDownItems.Add(new ToolStripSeparator());
            itemEdit.DropDownItems.Add(itemEdit_Duplicate);
            itemEdit.DropDownItems.Add(itemEdit_Rename);
            itemEdit.DropDownItems.Add(itemEdit_Delete);
            itemEdit.DropDownItems.Add(new ToolStripSeparator());
            itemEdit.DropDownItems.Add(itemEdit_Preferences);

            return itemEdit;
        }

        private ToolStripMenuItem BuildMenuComponent()
        {
            var itemComponent = new ToolStripMenuItem("Component");

            var itemComponent_Lamp = new ToolStripMenuItem("Lamp");
            var itemComponent_LED = new ToolStripMenuItem("LED");
            var itemComponent_Reel = new ToolStripMenuItem("Reel");
            var itemComponent_Display = new ToolStripMenuItem("Display");
            var itemComponent_Button = new ToolStripMenuItem("Button");
            var itemComponent_Screen = new ToolStripMenuItem("Screen");
            var itemComponent_Label = new ToolStripMenuItem("Label");
            var itemComponent_Image = new ToolStripMenuItem("Image");

            var itemComponent_Group = new ToolStripMenuItem("Group");

            itemComponent.DropDownItems.Add(itemComponent_Lamp);
            itemComponent.DropDownItems.Add(itemComponent_LED);
            itemComponent.DropDownItems.Add(itemComponent_Reel);
            itemComponent.DropDownItems.Add(itemComponent_Display);
            itemComponent.DropDownItems.Add(itemComponent_Button);
            itemComponent.DropDownItems.Add(itemComponent_Screen);
            itemComponent.DropDownItems.Add(itemComponent_Label);
            itemComponent.DropDownItems.Add(itemComponent_Image);
            itemComponent.DropDownItems.Add(new ToolStripSeparator());
            itemComponent.DropDownItems.Add(itemComponent_Group);

            return itemComponent;
        }

        private ToolStripMenuItem BuildMenuHelp()
        {
            var itemHelp = new ToolStripMenuItem("Help");

            var itemHelp_ContextHelp = new ToolStripMenuItem("Context Help");
            var itemHelp_Manual = new ToolStripMenuItem("Manual");

            var itemHelp_CheckForUpdates = new ToolStripMenuItem("Check for Updates");

            var itemHelp_About = new ToolStripMenuItem("About");

            itemHelp.DropDownItems.Add(itemHelp_ContextHelp);
            itemHelp.DropDownItems.Add(itemHelp_Manual);
            itemHelp.DropDownItems.Add(new ToolStripSeparator());
            itemHelp.DropDownItems.Add(itemHelp_CheckForUpdates);
            itemHelp.DropDownItems.Add(new ToolStripSeparator());
            itemHelp.DropDownItems.Add(itemHelp_About);

            return itemHelp;
        }

        private void BuildHierarchy()
        {
            BuildTabControl();
            BuildTabPage();

            _treeView = new TreeView();
            _treeView.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            //_treeView.Location = new Point(0, uwfHeaderHeight - 1); // All controls should be placed with Form header offset.
            //_treeView.Location = new Point(0, uwfHeaderHeight - 1 + _menuStrip.Height);
            //_treeView.Height = Height - uwfHeaderHeight + 1 - _menuStrip.Height;
            //_treeView.TabStop = false;
            _treeView.Size = _tabControl.Size;
            //_treeView.BackColor = Color.Aquamarine;
            //_treeView.NodeMouseClick += TreeViewOnNodeMouseClick;

            BuildHierarchyData();



            // for now, just create a single tabbed pane, later implement dockable tab system
            //_treeViewPanel.Controls.Add(_treeView);
            _tabPage.Controls.Add(_treeView);
            _tabControl.TabPages.Add(_tabPage);
            Controls.Add(_tabControl);
        }

        private void BuildTabControl()
        {
            _tabControl = new TabControl();

            _tabControl.Location = new Point(0, uwfHeaderHeight - 1 + _menuStrip.Height);
            //_treeViewPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            _tabControl.Height = Height - uwfHeaderHeight + 1 - _menuStrip.Height;
            _tabControl.Width = kTreeViewWidth;
            //_treeView.TabStop = false;
            //_treeView.Width = 220;
            _tabControl.Padding = new Padding(0);
        }

        private void BuildTabPage()
        {
            _tabPage = new TabPage();
            _tabPage.Size = _tabControl.Size;
            _tabPage.Text = "Hierarchy";
        }

        private void BuildHierarchyData()
        {
            // TODO replace the null with tags

            var nodeButtons = new TreeNode("Buttons");
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            AddNode(nodeButtons, "Button", null);
            _treeView.Nodes.Add(nodeButtons);

            var nodeLamps = new TreeNode("Lamps");
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            AddNode(nodeLamps, "Lamp", null);
            _treeView.Nodes.Add(nodeLamps);

            var nodeSegmentDisplays = new TreeNode("Segment Displays");
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            AddNode(nodeSegmentDisplays, "Segment Display", null);
            _treeView.Nodes.Add(nodeSegmentDisplays);

            // Refresh method or ExpandAll will update view list. 
            // NOTE: most controls don't need to be refreshed. Make sure to take a look 
            // at Refresh implementation in Control that you think is not working.
            _treeView.ExpandAll();

            // Grip renderer is normal control. Bring it to front if you use it over other controls that can technically hide it.
            // uwfSizeGripRenderer.BringToFront();

        }

        private void Build2D()
        {
        }

        private void BuildInspector()
        {
        }

        private static void AddNode(TreeNode parent, string text, object tag)
        {
            var node = new TreeNode(text);
            node.Tag = tag;

            parent.Nodes.Add(node);
        }
        private void TreeViewOnNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            //if (e.Button != MouseButtons.Left || e.Node == null || e.Node.Tag == null)
            //    return;

            //var panelType = e.Node.Tag as Type;
            //if (panelType == null)
            //    return;

            //var panel = Activator.CreateInstance(panelType) as BaseExamplePanel;
            //if (panel == null)
            //    return;

            //SetPanel(panel);
        }
    }
}
