using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oasis.Utility
{
    public static class PngHelper 
    {
        public static Texture2D LoadPngToTexture2D(byte[] fileBytes)
        {
            Texture2D texture = new(2, 2);
            if (texture.LoadImage(fileBytes)) // Load the image data into the texture (this will auto-resize the texture dimensions)
            {
                return texture;
            }
            else
            {
                Debug.LogError("Failed to load texture from file data.");
            }

            return null;
        }

        public static Texture2D LoadPngToTexture2D(string filePath)
        {
            if (File.Exists(filePath))
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                return LoadPngToTexture2D(fileBytes);
            }
            else
            {
                Debug.LogError("File not found at: " + filePath);
            }

            return null;
        }
    }
}
