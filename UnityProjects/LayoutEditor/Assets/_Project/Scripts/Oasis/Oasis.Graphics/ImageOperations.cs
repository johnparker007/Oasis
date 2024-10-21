using System;
using System.IO;
using System.Drawing;
using UnityEngine;

namespace Oasis.Graphics {
    public class ImageOperations 
    {
        public static void SaveToPNG(OasisImage image, string fileUniqueName)
        {
            //TODO: Get rid of the ugly hardwiring here.
            string fileName = string.Format("e:\\SavedLayout\\{0}", fileUniqueName);
            File.WriteAllBytes(
                fileName,
                ImageConversion.EncodeArrayToPNG(
                    image.GetAsByteArray(),
                    UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_SRGB,
                    (uint)image.Width,
                    (uint)image.Height
                )
            );
        }

        public static OasisImage LoadFromPng(string filePath)
        {
            return new OasisImage(File.ReadAllBytes(filePath));
        }
    }
}
