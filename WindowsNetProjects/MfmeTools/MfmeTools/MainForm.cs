using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MfmeTools
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void OnButtonStartExtractionClick(object sender, EventArgs e)
        {
            Extractor.Options extractorOptions = new Extractor.Options()
            {
                SourceLayoutPath = textBoxExtractSourcePath.Text,
                UseCachedLampImages = checkBoxUseCachedLampImages.Checked,
                UseCachedReelImages = checkBoxUseCachedReelImages.Checked,
                ScrapeLamps5To8 = checkBoxScrapeLamps5_8.Checked,
                ScrapeLamps9To12 = checkBoxScrapeLamps9_12.Checked
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
