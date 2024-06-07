using Oasis.Utility;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oasis.MAME
{
    public class MameMpu4ChrSourceCodeLookup : MonoBehaviour
    {
        private const string kSourceCodeDirectoryPath = "MameSource\\Barcrest";

        public string LampColumnDataSourceFilename;
        public string[] RomLampColumnReferenceSourceFilenames;

        private bool _initialised = false;

        // mame variable name, lamp column hex strings[8]
        private Dictionary<string, string[]> _lampColumnDataDictionary = null;
        // mame rom name, mame variable name
        private Dictionary<string, string> _romDataReferencesDictionary = null;

        public string SourceCodeDirectoryFullPath
        {
            get
            {
                return Path.Combine(DataPathHelper.DynamicRootPath, kSourceCodeDirectoryPath);
            }
        }

        public string[] GetLampColumnData(string mameRomName)
        {
            if(!_initialised)
            {
                Initialise();
            }

            if(_romDataReferencesDictionary.ContainsKey(mameRomName))
            {
                string mameVariableName = _romDataReferencesDictionary[mameRomName];
                if (_lampColumnDataDictionary.ContainsKey(mameVariableName))
                {
                    return _lampColumnDataDictionary[mameRomName];
                }
            }

            return null;
        }

        private void Initialise()
        {
            InitialiseLampColumnData();
            InitialiseRomDataReferences();

            _initialised = true;
        }

        private void InitialiseLampColumnData()
        {
            _lampColumnDataDictionary = new Dictionary<string, string[]>();

            string lampColumnDataPath = Path.Combine(SourceCodeDirectoryFullPath, LampColumnDataSourceFilename);
            string[] lines = File.ReadAllLines(lampColumnDataPath);

            foreach(string line in lines)
            {
                if(!line.Contains("[8]"))
                {
                    continue;
                }

                if(CheckIfComment(line))
                {
                    continue;
                }

                AddLampColumnDataRow(line);
            }
        }

        private void AddLampColumnDataRow(string line)
        {
            string mameVariableName = ExtractMameVariableName(line);
            string[] lampColumnData = ExtractLampColumnData(line);

            _lampColumnDataDictionary.Add(mameVariableName, lampColumnData);
        }

        private string ExtractMameVariableName(string line)
        {
            int endIndex = line.LastIndexOf('[');
            int startIndex = 0;
            for(int index = endIndex; index > 0; --index)
            {
                if(line[index] == ' ')
                {
                    startIndex = index + 1;
                    break;
                }
            }

            int length = endIndex - startIndex;

            return line.Substring(startIndex, length);
        }

        private string[] ExtractLampColumnData(string line)
        {
            int startIndex = line.IndexOf('{') + 1;
            int endIndex = line.LastIndexOf('}');
            int length = endIndex - startIndex;

            string hexValuesUnsplit = line.Substring(startIndex, length);
            string[] hexValuesSplit = hexValuesUnsplit.Split(',');

            for(int hexValueIndex = 0; hexValueIndex < hexValuesSplit.Length; ++hexValueIndex)
            {
                hexValuesSplit[hexValueIndex] = hexValuesSplit[hexValueIndex].Trim();
                // TOIMPROVE should do sanity check length is 4 chars and first 2 chars are "0x"
                hexValuesSplit[hexValueIndex] = hexValuesSplit[hexValueIndex].Substring(2);
            }

            return hexValuesSplit;
        }

        private bool CheckIfComment(string line)
        {
            return line.Trim().StartsWith('/');
        }

        private void InitialiseRomDataReferences()
        {
            // TODO
        }
    }
}
