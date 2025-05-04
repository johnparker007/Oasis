using System;
using System.IO;
using System.Drawing;
using UnityEngine;

namespace Oasis.Graphics {
    public class ImageOperations 
    {
        public static void SaveToPNG(OasisImage image, string fileUniqueName)
        {
            //JP TODO slightly less hardwiring, but we will prob want to pass in a path
            // to this generic SaveToPNG function:
            string filePath = Path.Combine(
                                  Editor.Instance.ProjectsController.ProjectRootPath,
                                  fileUniqueName);

            File.WriteAllBytes(
                filePath,
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
