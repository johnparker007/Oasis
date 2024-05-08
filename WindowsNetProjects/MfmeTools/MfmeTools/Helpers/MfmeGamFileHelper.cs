using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System;


namespace Oasis.MfmeTools.Helpers
{
    public static class MfmeGamFileHelper
    {
        public static readonly string kKeyLayout = "Layout";
        public static readonly string kKeyPlatform = "System";

        public static string GetFmlFilename(string gamFilePath)
        {
            return GetValue(gamFilePath, kKeyLayout);
        }

        public static string GetPlatformName(string gamFilePath)
        {
            return GetValue(gamFilePath, kKeyPlatform);
        }

        public static string GetValue(string gamFilePath, string key)
        {
            // can't use ReadAllLines, as it doesn't read characters above 127, but 
            // MFME writes characters above 127 (e.g: £ character was encoded as $A3):
            string contents = File.ReadAllText(gamFilePath, System.Text.Encoding.GetEncoding(1252));
            string[] lines = contents.Split('\n');

            foreach (string line in lines)
            
        {
                if (line.StartsWith(key))
                {
                    return line.Substring(key.Length + 1); // +1 for the Space char after the key name in the gam file
                }
            }

            OutputLog.LogError("key '" + key + "' not found in gam file: " + gamFilePath);

            return null;
        }

        public static void PatchLines(string gamFilePath, string key, string patchText, bool preserveKey)
        {
            string[] lines = File.ReadAllLines(gamFilePath);

            for (int lineIndex = 0; lineIndex < lines.Length; ++lineIndex)
            {
                if (lines[lineIndex].StartsWith(key))
                {
                    if(preserveKey)
                    {
                        lines[lineIndex] = key + " " + patchText;
                    }
                    else
                    {
                        lines[lineIndex] = patchText;
                    }
                }
            }

            File.WriteAllLines(gamFilePath, lines);

            OutputLog.Log($"Patched .gam file: [{key}]->[{patchText}]");
        }

    }

}
