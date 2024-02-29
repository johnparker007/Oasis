using MfmeTools.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MfmeTools
{
    class Configuration
    {
        public string BrowseExtractionSourcePath()
        {
            //string initialDirectory = _configurationController.GetConfigurationsPath();
            //initialDirectory = initialDirectory.Replace("/", "\\"); // needed!
            string initialDirectory = "C:\\";
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                InitialDirectory = initialDirectory,
                Filter = "MFME Game Files (*.gam)|*.gam",
                DefaultExt = ".gam"
            };

            FileHelper.UseDefaultExtAsFilterIndex(openFileDialog);

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string configurationPath = openFileDialog.FileName;
                return configurationPath;
                //_configurationController.LoadConfiguration(configurationPath);

                //PopulateFormDataFromConfiguration(_configurationController.CurrentConfiguration);

                //LampRoutineOptionsPanel.Enabled = false;
                //GameLampLookupComboBox.SelectedIndex = -1;
            }

            //if (_configurationController.CurrentConfiguration != null)
            //{
            //    ProcessSourceRom(_configurationController.CurrentConfiguration.SelectedROMPath);
            //}

            return null;
        }
    }
}
