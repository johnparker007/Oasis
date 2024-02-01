namespace Oasis.UI.Views
{
    public class ViewMenu : ViewBase
    {
        //public MenuStrip MenuStrip
        //{
        //    get;
        //    private set;
        //} = null;

        //public Panel Panel
        //{
        //    get;
        //    private set;
        //} = null;

        //protected ViewModelMenu ViewModelMenu
        //{
        //    get
        //    {
        //        return (ViewModelMenu)_viewModel;
        //    }
        //}

        //public ViewMenu(RootUI rootUI, Control parent, ViewModel viewModel) : base(rootUI, parent, viewModel)
        //{
        //    MenuStrip = new MenuStrip();
        //    BuildMenuPanel();

        //    MenuStrip.Items.Add(BuildMenuFile());
        //    MenuStrip.Items.Add(BuildMenuEdit());
        //    MenuStrip.Items.Add(BuildMenuComponent());
        //    MenuStrip.Items.Add(BuildMenuEmulation());
        //    MenuStrip.Items.Add(BuildMenuHelp());

        //    MenuStrip.BackColor = Color.FromArgb(56, 56, 56); // JP Dark theme

        //    Panel.Controls.Add(MenuStrip);

        //    // DON'T ADD AS REPLACEING WITH NATIVE MENUS:
        //    //_parent.Controls.Add(Panel);

        //    // for ref:
        //    ////itemFile_New.ShortcutKeys = Keys.Control | Keys.N;
        //    ////itemFile_Save.ShortcutKeys = Keys.Control | Keys.S;
        //    ////itemFile_Exit.ShortcutKeys = Keys.Control | Keys.W;

        //    //itemFile_Exit.Image = uwfAppOwner.Resources.Close;
        //    //itemFile_Exit.uwfImageColor = Color.FromArgb(64, 64, 64);
        //}

        //private void BuildMenuPanel()
        //{
        //    Panel = Activator.CreateInstance(typeof(Panel)) as Panel;

        //    Panel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        //    Panel.Height = MenuStrip.Height;
        //    Panel.Width = _parent.Width;
        //    Panel.BackColor = Color.FromArgb(56, 56, 56); // JP Dark theme
        //}

        //private ToolStripMenuItem BuildMenuFile()
        //{
        //    var itemFile = new ToolStripMenuItem("File");

        //    var itemFile_New = new ToolStripMenuItem("New...");
        //    var itemFile_Open = new ToolStripMenuItem("Open...");
        //    var itemFile_Save = new ToolStripMenuItem("Save...");
        //    var itemFile_SaveAs = new ToolStripMenuItem("Save As...");

        //    var itemFile_Extract = new ToolStripMenuItem("MFME Extract");
        //    itemFile_Extract.Click += ViewModelMenu.OnFile_ExtractClick;

        //    var itemFile_Import = new ToolStripMenuItem("MFME Import");
        //    itemFile_Import.Click += ViewModelMenu.OnFile_ImportClick;

        //    var itemFile_Export = new ToolStripMenuItem("MAME Export");
        //    itemFile_Export.Click += ViewModelMenu.OnFile_ExportClick;

        //    var itemFile_Close = new ToolStripMenuItem("Close");
        //    var itemFile_Exit = new ToolStripMenuItem("Exit");

        //    itemFile.DropDownItems.Add(itemFile_New);
        //    itemFile.DropDownItems.Add(itemFile_Open);
        //    itemFile.DropDownItems.Add(itemFile_Save);
        //    itemFile.DropDownItems.Add(itemFile_SaveAs);
        //    itemFile.DropDownItems.Add(new ToolStripSeparator());
        //    itemFile.DropDownItems.Add(itemFile_Extract);
        //    itemFile.DropDownItems.Add(itemFile_Import);
        //    itemFile.DropDownItems.Add(itemFile_Export);
        //    itemFile.DropDownItems.Add(new ToolStripSeparator());
        //    itemFile.DropDownItems.Add(itemFile_Close);
        //    itemFile.DropDownItems.Add(itemFile_Exit);
           
        //    return itemFile;
        //}

        //private ToolStripMenuItem BuildMenuEdit()
        //{
        //    var itemEdit = new ToolStripMenuItem("Edit");

        //    var itemEdit_Undo = new ToolStripMenuItem("Undo");
        //    var itemEdit_Redo = new ToolStripMenuItem("Redo");

        //    var itemEdit_Cut = new ToolStripMenuItem("Cut");
        //    var itemEdit_Copy = new ToolStripMenuItem("Copy");
        //    var itemEdit_Paste = new ToolStripMenuItem("Paste");

        //    var itemEdit_Duplicate = new ToolStripMenuItem("Duplicate");
        //    var itemEdit_Rename = new ToolStripMenuItem("Rename");
        //    var itemEdit_Delete = new ToolStripMenuItem("Delete");

        //    var itemEdit_Preferences = new ToolStripMenuItem("Preferences");

        //    itemEdit.DropDownItems.Add(itemEdit_Undo);
        //    itemEdit.DropDownItems.Add(itemEdit_Redo);
        //    itemEdit.DropDownItems.Add(new ToolStripSeparator());
        //    itemEdit.DropDownItems.Add(itemEdit_Cut);
        //    itemEdit.DropDownItems.Add(itemEdit_Copy);
        //    itemEdit.DropDownItems.Add(itemEdit_Paste);
        //    itemEdit.DropDownItems.Add(new ToolStripSeparator());
        //    itemEdit.DropDownItems.Add(itemEdit_Duplicate);
        //    itemEdit.DropDownItems.Add(itemEdit_Rename);
        //    itemEdit.DropDownItems.Add(itemEdit_Delete);
        //    itemEdit.DropDownItems.Add(new ToolStripSeparator());
        //    itemEdit.DropDownItems.Add(itemEdit_Preferences);

        //    return itemEdit;
        //}

        //private ToolStripMenuItem BuildMenuComponent()
        //{
        //    var itemComponent = new ToolStripMenuItem("Component");

        //    var itemComponent_Lamp = new ToolStripMenuItem("Lamp");
        //    var itemComponent_LED = new ToolStripMenuItem("LED");
        //    var itemComponent_Reel = new ToolStripMenuItem("Reel");
        //    var itemComponent_Display = new ToolStripMenuItem("Display");
        //    var itemComponent_Button = new ToolStripMenuItem("Button");
        //    var itemComponent_Screen = new ToolStripMenuItem("Screen");
        //    var itemComponent_Label = new ToolStripMenuItem("Label");
        //    var itemComponent_Image = new ToolStripMenuItem("Image");

        //    var itemComponent_Group = new ToolStripMenuItem("Group");

        //    itemComponent.DropDownItems.Add(itemComponent_Lamp);
        //    itemComponent.DropDownItems.Add(itemComponent_LED);
        //    itemComponent.DropDownItems.Add(itemComponent_Reel);
        //    itemComponent.DropDownItems.Add(itemComponent_Display);
        //    itemComponent.DropDownItems.Add(itemComponent_Button);
        //    itemComponent.DropDownItems.Add(itemComponent_Screen);
        //    itemComponent.DropDownItems.Add(itemComponent_Label);
        //    itemComponent.DropDownItems.Add(itemComponent_Image);
        //    itemComponent.DropDownItems.Add(new ToolStripSeparator());
        //    itemComponent.DropDownItems.Add(itemComponent_Group);

        //    return itemComponent;
        //}

        //private ToolStripMenuItem BuildMenuEmulation()
        //{
        //    var itemEmulation = new ToolStripMenuItem("Emulation");

        //    var itemEmulation_Start = new ToolStripMenuItem("Start");
        //    itemEmulation_Start.Click += ViewModelMenu.OnEmulation_StartClick;

        //    var itemEmulation_Exit = new ToolStripMenuItem("Exit");
        //    itemEmulation_Exit.Click += ViewModelMenu.OnEmulation_ExitClick;

        //    var itemEmulation_Pause = new ToolStripMenuItem("Pause");
        //    itemEmulation_Pause.Click += ViewModelMenu.OnEmulation_PauseClick;

        //    var itemEmulation_Resume = new ToolStripMenuItem("Resume");
        //    itemEmulation_Resume.Click += ViewModelMenu.OnEmulation_ResumeClick;

        //    var itemEmulation_SoftReset = new ToolStripMenuItem("Soft Reset");
        //    itemEmulation_SoftReset.Click += ViewModelMenu.OnEmulation_SoftResetClick;

        //    var itemEmulation_HardReset = new ToolStripMenuItem("Hard Reset");
        //    itemEmulation_HardReset.Click += ViewModelMenu.OnEmulation_HardResetClick;

        //    var itemEmulation_Throttled = new ToolStripMenuItem("Throttled");
        //    itemEmulation_Throttled.Click += ViewModelMenu.OnEmulation_ThrottledClick;

        //    var itemEmulation_Unthrottled = new ToolStripMenuItem("Unthrottled");
        //    itemEmulation_Unthrottled.Click += ViewModelMenu.OnEmulation_UnthrottledClick;

        //    var itemEmulation_StateLoad = new ToolStripMenuItem("State Load");
        //    itemEmulation_StateLoad.Click += ViewModelMenu.OnEmulation_StateLoadClick;

        //    var itemEmulation_StateSave = new ToolStripMenuItem("State Save");
        //    itemEmulation_StateSave.Click += ViewModelMenu.OnEmulation_StateSaveClick;

        //    var itemEmulation_StateSaveAndExit = new ToolStripMenuItem("State Save And Exit");
        //    itemEmulation_StateSaveAndExit.Click += ViewModelMenu.OnEmulation_StateSaveAndExitClick;

        //    var itemEmulation_StartAndStateLoad = new ToolStripMenuItem("Start And State Load");
        //    itemEmulation_StartAndStateLoad.Click += ViewModelMenu.OnEmulation_StartAndStateLoadClick;

        //    itemEmulation.DropDownItems.Add(itemEmulation_StartAndStateLoad);
        //    itemEmulation.DropDownItems.Add(itemEmulation_StateSaveAndExit);
        //    itemEmulation.DropDownItems.Add(new ToolStripSeparator());
        //    itemEmulation.DropDownItems.Add(itemEmulation_Start);
        //    itemEmulation.DropDownItems.Add(itemEmulation_StateLoad);
        //    itemEmulation.DropDownItems.Add(itemEmulation_StateSave);
        //    itemEmulation.DropDownItems.Add(itemEmulation_Exit);
        //    itemEmulation.DropDownItems.Add(new ToolStripSeparator());
        //    itemEmulation.DropDownItems.Add(itemEmulation_Pause);
        //    itemEmulation.DropDownItems.Add(itemEmulation_Resume);
        //    itemEmulation.DropDownItems.Add(new ToolStripSeparator());
        //    itemEmulation.DropDownItems.Add(itemEmulation_Throttled);
        //    itemEmulation.DropDownItems.Add(itemEmulation_Unthrottled);
        //    itemEmulation.DropDownItems.Add(new ToolStripSeparator());
        //    itemEmulation.DropDownItems.Add(itemEmulation_SoftReset);
        //    itemEmulation.DropDownItems.Add(itemEmulation_HardReset);

        //    return itemEmulation;
        //}

        //private ToolStripMenuItem BuildMenuHelp()
        //{
        //    var itemHelp = new ToolStripMenuItem("Help");

        //    var itemHelp_ContextHelp = new ToolStripMenuItem("Context Help");
        //    var itemHelp_Manual = new ToolStripMenuItem("Manual");

        //    var itemHelp_CheckForUpdates = new ToolStripMenuItem("Check for Updates");

        //    var itemHelp_About = new ToolStripMenuItem("About");
        //    itemHelp_About.Click += ViewModelMenu.OnHelp_AboutClick;

        //    itemHelp.DropDownItems.Add(itemHelp_ContextHelp);
        //    itemHelp.DropDownItems.Add(itemHelp_Manual);
        //    itemHelp.DropDownItems.Add(new ToolStripSeparator());
        //    itemHelp.DropDownItems.Add(itemHelp_CheckForUpdates);
        //    itemHelp.DropDownItems.Add(new ToolStripSeparator());
        //    itemHelp.DropDownItems.Add(itemHelp_About);

        //    return itemHelp;
        //}
    }

}
