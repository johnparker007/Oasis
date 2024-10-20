using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Oasis.MfmeTools
{
    public partial class MainForm : Form
    {
        public static bool kDebugHardcodePopulateSourceGamPath = true;
        public static bool kDebugDefaultCacheAllImageTypes = true;
        public static bool kDebugDefaultCacheRomIdent = true;
        public static bool kDebugDefaultDisableFontSmoothing = true;



        public RichTextBox OutputLogRichTextBox
        {
            get
            {
                return richTextBoxOutputLog;
            }
        }

        public MainForm()
        {
            InitializeComponent();

            if(kDebugHardcodePopulateSourceGamPath)
            {
                //textBoxExtractSourcePath.Text = 
                //    "C:\\projects\\ChrFreeRomAutoPatcher_RomsAndLayouts\\LegacySectionFromDif\\Unzipped\\Barcrest\\Andy Capp (Barcrest)\\Andy_Capp_(Barcrest)_[Dx08_6jp].gam";

                textBoxExtractSourcePath.Text =
                    "C:\\Users\\John\\Desktop\\_temp\\Nickelodeon £3 All Cash Dx\\Nickelodeon £3 All Cash.gam";

            
            }

            if(kDebugDefaultCacheAllImageTypes)
            {
                checkBoxUseCachedReelImages.CheckState = CheckState.Checked;
                checkBoxUseCachedLampImages.CheckState = CheckState.Checked;
                checkBoxUseCachedButtonImages.CheckState = CheckState.Checked;
                checkBoxUseCachedBitmapImages.CheckState = CheckState.Checked;
                checkBoxUseCachedBackgroundImage.CheckState = CheckState.Checked;
            }

            if(kDebugDefaultCacheRomIdent)
            {
                checkBoxUseCachedRomIdent.CheckState = CheckState.Checked;
            }

            if(kDebugDefaultDisableFontSmoothing)
            {
                checkBoxDisableFontSmoothing.CheckState = CheckState.Checked;
            }
        }

        private void OnButtonStartExtractionClick(object sender, EventArgs e)
        {
            Extractor.Options extractorOptions = new Extractor.Options()
            {
                SourceLayoutPath = textBoxExtractSourcePath.Text,

                UseCachedLampImages = checkBoxUseCachedLampImages.Checked,
                UseCachedReelImages = checkBoxUseCachedReelImages.Checked,
                UseCachedButtonImages = checkBoxUseCachedButtonImages.Checked,
                UseCachedBitmapImages = checkBoxUseCachedBitmapImages.Checked,
                UseCachedBackgroundImage = checkBoxUseCachedBackgroundImage.Checked,
                UseCachedRomIdent = checkBoxUseCachedRomIdent.Checked,

                ScrapeLamps5To8 = checkBoxScrapeLamps5_8.Checked,
                ScrapeLamps9To12 = checkBoxScrapeLamps9_12.Checked,

                DisableFontSmoothing = checkBoxDisableFontSmoothing.Checked
            };

            Program.Extractor.StartExtraction(extractorOptions);
        }

        private void OnButtonExtractSourcePathClick(object sender, EventArgs e)
        {
            string sourcePath = Program.Configuration.BrowseExtractionSourcePath();

            if(sourcePath != null)
            {
                textBoxExtractSourcePath.Text = sourcePath;
            }
        }
    }
}
