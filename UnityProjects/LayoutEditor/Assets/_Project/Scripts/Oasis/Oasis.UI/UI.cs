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
            SetWindowState(FormWindowState.Maximized);
            MaximizeBox = false;
            ControlBox = false;
            AutoScroll = true;
            BackColor = Color.FromArgb(255, 0, 255);
            Text = "Oasis Desktop";

            var panel = Activator.CreateInstance(typeof(PanelMenuStrip)) as PanelMenuStrip;
            if (panel == null)
                return;
            if (_currentPanel != null && !_currentPanel.IsDisposed)
                _currentPanel.Dispose();

            _currentPanel = panel;
            _currentPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            _currentPanel.Location = new Point(0, uwfHeaderHeight);
            _currentPanel.Height = Height - uwfHeaderHeight - 16; // We don't want to hide SizeGripRenderer with scrollbars.
            _currentPanel.Height = Width;
            //_currentPanel.Width = Width - treeView.Width;

            //_currentPanel.Show();


            BuildMenu();

            Controls.Add(_currentPanel);

            //_currentPanel.Initialize();


        }

        private void BuildMenu()
        {
            var itemFile_New = new ToolStripMenuItem("New");
            var itemFile_Open = new ToolStripMenuItem("Open");
            var itemFile_Save = new ToolStripMenuItem("Save");
            var itemFile_Exit = new ToolStripMenuItem("Exit");

            itemFile_New.ShortcutKeys = Keys.Control | Keys.N;
            itemFile_Save.ShortcutKeys = Keys.Control | Keys.S;
            itemFile_Exit.ShortcutKeys = Keys.Control | Keys.W;

            itemFile_Open.DropDownItems.Add(new ToolStripMenuItem("file1.txt"));
            itemFile_Open.DropDownItems.Add(new ToolStripMenuItem("file2.txt"));
            itemFile_Open.DropDownItems.Add(new ToolStripMenuItem("file3.txt"));
            itemFile_Open.DropDownItems.Add(new ToolStripSeparator());
            itemFile_Open.DropDownItems.Add(new ToolStripMenuItem("last"));

            itemFile_Exit.Image = uwfAppOwner.Resources.Close;
            itemFile_Exit.uwfImageColor = Color.FromArgb(64, 64, 64);

            var itemEdit_Undo = new ToolStripMenuItem("Undo");
            var itemEdit_Redo = new ToolStripMenuItem("Redo");

            itemEdit_Undo.ShortcutKeys = Keys.Control | Keys.Z;
            itemEdit_Redo.ShortcutKeys = Keys.Control | Keys.Y;

            var itemFile = new ToolStripMenuItem("File");
            var itemEdit = new ToolStripMenuItem("Edit");
            var itemView = new ToolStripMenuItem("View");

            itemFile.DropDownItems.Add(itemFile_New);
            itemFile.DropDownItems.Add(itemFile_Open);
            itemFile.DropDownItems.Add(itemFile_Save);
            itemFile.DropDownItems.Add(new ToolStripSeparator());
            itemFile.DropDownItems.Add(itemFile_Exit);

            itemEdit.DropDownItems.Add(itemEdit_Undo);
            itemEdit.DropDownItems.Add(itemEdit_Redo);

            itemView.DropDownItems.Add(new ToolStripMenuItem("nothing"));

            var menu = new MenuStrip();
            menu.Items.Add(itemFile);
            menu.Items.Add(itemEdit);
            menu.Items.Add(itemView);

            _currentPanel.Controls.Add(menu);
        }
    }
}
