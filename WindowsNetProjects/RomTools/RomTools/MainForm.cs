using Oasis.RomTools.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Oasis.RomTools
{
    public partial class MainForm : Form
    {
        public class Block
        {
            public int StartOffset;
            public int Length;
        }


        // TODO get these into the UI and config
        const int kStartPadding = 8;
        const int kEndPadding = 8;

        private List<Block> _potentialBlocks = new List<Block>();

        public MainForm()
        {
            InitializeComponent();
        }


        private void buttonOriginalRomPath_Click(object sender, EventArgs e)
        {
            string sourcePath = BrowseRomPath();

            if (sourcePath != null)
            {
                textBoxOriginalRomPath.Text = sourcePath;
            }
        }

        private void buttonWorkingRomPath_Click(object sender, EventArgs e)
        {
            string sourcePath = BrowseRomPath();

            if (sourcePath != null)
            {
                textBoxWorkingRomPath.Text = sourcePath;
            }
        }

        public string BrowseRomPath()
        {
            //string initialDirectory = _configurationController.GetConfigurationsPath();
            //initialDirectory = initialDirectory.Replace("/", "\\"); // needed!
            string initialDirectory = "C:\\";
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                InitialDirectory = initialDirectory,
                Filter = "ROM Files (*.*)|*.*",
                DefaultExt = ".*"
            };

            FileHelper.UseDefaultExtAsFilterIndex(openFileDialog);

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string configurationPath = openFileDialog.FileName;
                return configurationPath;
            }

            return null;
        }

        private void buttonCreatPatchedRom_Click(object sender, EventArgs e)
        {
            string originalRomPath = textBoxOriginalRomPath.Text;
            string workingRomPath = textBoxWorkingRomPath.Text;

            byte[] originalBytes = File.ReadAllBytes(originalRomPath);
            byte[] workingBytes = File.ReadAllBytes(workingRomPath);

            int totalSumDifference = GetTotalSumDifference(originalBytes, workingBytes);
            int patchLength = Math.Abs(totalSumDifference) < 256 ? 1 : 2;

            List<Block> blocks = new List<Block>();
            if (totalSumDifference > 0)
            {
                blocks = FindBlocks(originalBytes, 0xFF, patchLength);
            }
            else if(totalSumDifference < 0)
            {
                blocks = FindBlocks(originalBytes, 0x00, patchLength);
            }
            else
            {
                // TODO no patch required message
            }

            byte[] outputBytes = new byte[workingBytes.Length];
            Array.Copy(workingBytes, outputBytes, workingBytes.Length); ;
            if (totalSumDifference != 0)
            {
                PatchChecksum(outputBytes, blocks.Last(), totalSumDifference);
            }

            File.WriteAllBytes(workingRomPath + "_PATCHED", outputBytes);
        }

        private int GetTotalSumDifference(byte[] originalBytes, byte[] workingBytes)
        {
            ulong originalTotalSum = ChecksumHelper.GetTotalSum(originalBytes);
            ulong workingTotalSum = ChecksumHelper.GetTotalSum(workingBytes);

            int totalSumDifference = (int)(workingTotalSum - originalTotalSum);

            return totalSumDifference;
        }


        private List<Block> FindBlocks(byte[] dataBytes, byte fillValue, int patchLength)
        {
            List<Block> results = new List<Block>();

            int minimumLength = kStartPadding + patchLength + kEndPadding;

            int searchStartOffset = 0x0;
            int foundOffset = 0;
            int foundLength = 0;
            bool complete = false;
            do
            {
                if (FindBlockOffset(dataBytes, fillValue, searchStartOffset, minimumLength, ref foundOffset, ref foundLength))
                {
                    results.Add(new Block() { StartOffset = foundOffset, Length = foundLength });
                    searchStartOffset = foundOffset + foundLength;
                }
                else
                {
                    complete = true;
                }
            }
            while (!complete);

            return results;
        }

        private bool FindBlockOffset(
            byte[] dataBytes,
            byte fillValue, int startOffset, int minimumLength, ref int foundOffset, ref int foundLength)
        {
            int matchCount = 0;
            int offset;
            for (offset = startOffset; offset < dataBytes.Length; ++offset)
            {
                if (dataBytes[offset] == fillValue)
                {
                    ++matchCount;
                }
                else
                {
                    if (matchCount >= minimumLength)
                    {
                        foundOffset = offset - matchCount;
                        foundLength = matchCount;
                        return true;
                    }

                    matchCount = 0;
                }
            }

            if (matchCount >= minimumLength)
            {
                foundOffset = offset - matchCount;
                foundLength = matchCount;
                return true;
            }

            return false;
        }

        private byte[] PatchChecksum(byte[] outputBytes, Block block, int totalSumDifference)
        {
            int byteValue = outputBytes[block.StartOffset + kStartPadding];
            outputBytes[block.StartOffset + kStartPadding] = (byte)(byteValue - totalSumDifference);

            return outputBytes;
        }
    }
}
