namespace Oasis.UI
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;
    using UnityWinForms.Examples.Panels;

    public class UI : Form
    {
        private Panel _currentPanel;
        public UI()
        {
            uwfHeaderHeight = 0;

            Size = new Size(Screen.width, Screen.height);
            MaximizeBox = false;
            ControlBox = false;
            AutoScroll = true;
            BackColor = Color.FromArgb(255, 0, 255);
            Text = "Oasis Desktop  - TODO modify form to allow this head bar to be removed?  Or use Container";

            SetWindowState(FormWindowState.Maximized);

            //var panel = Activator.CreateInstance(typeof(PanelMenuStrip)) as PanelMenuStrip;

            var panel = Activator.CreateInstance(typeof(Panel)) as Panel;
            if (panel == null)
                return;

            if (_currentPanel != null && !_currentPanel.IsDisposed)
                _currentPanel.Dispose();

            _currentPanel = panel;
            _currentPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            //_currentPanel.Location = new Point(0, uwfHeaderHeight);
            _currentPanel.Height = Height - uwfHeaderHeight;
            _currentPanel.Width = Width;

            BuildMenu();

            Controls.Add(_currentPanel);
        }

        private void BuildMenu()
        {
            var menu = new MenuStrip();
            menu.Items.Add(BuildMenuFile());
            menu.Items.Add(BuildMenuEdit());
            menu.Items.Add(BuildMenuComponent());
            menu.Items.Add(BuildMenuHelp());

            _currentPanel.Controls.Add(menu);

            // for ref:
            ////itemFile_New.ShortcutKeys = Keys.Control | Keys.N;
            ////itemFile_Save.ShortcutKeys = Keys.Control | Keys.S;
            ////itemFile_Exit.ShortcutKeys = Keys.Control | Keys.W;

            //itemFile_Exit.Image = uwfAppOwner.Resources.Close;
            //itemFile_Exit.uwfImageColor = Color.FromArgb(64, 64, 64);
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
    }
}