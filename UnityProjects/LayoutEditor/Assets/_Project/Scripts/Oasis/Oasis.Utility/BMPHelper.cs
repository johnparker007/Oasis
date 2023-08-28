using System.Collections;
using System.Collections.Generic;
using System.IO;
using B83.Image.BMP;
//using OpenCVForUnity.CoreModule;
using UnityEngine;

// ******* TODO NEED TO SPLIT OUT TO RUNTIME/EDITOR COMPATIBLE CLASSES

public class BMPHelper 
{
    public static void ReadBmpAndWritePng(
        string bmpFilePath, 
        string bmpMaskFilePath,
        string pngFilepath, 
        bool pngFilePathIsWithinAssets,
        string pngFilename, 
        out Vector2Int size, 
        bool mirrorX,
        bool mirrorY, 
        bool rotate,
        bool transparent = false)
    {
        BMPLoader bmpLoader = new BMPLoader();

        bmpLoader.ForceAlphaReadWhenPossible = transparent;
        bmpLoader.ReadPaletteAlpha = transparent;

        BMPImage bmpImage = bmpLoader.LoadBMP(bmpFilePath);
        BMPImage bmpMaskImage = null;
        if(bmpMaskFilePath != null)
        {
            bmpMaskImage = bmpLoader.LoadBMP(bmpMaskFilePath);
        }

        size = Vector2Int.zero;
        size.x = bmpImage.info.absWidth;
        size.y = bmpImage.info.absHeight;

        int textureWidth;
        int textureHeight;
        if(rotate)
        {
            textureWidth = bmpImage.info.absHeight;
            textureHeight = bmpImage.info.absWidth;
        }
        else
        {
            textureWidth = bmpImage.info.absWidth;
            textureHeight = bmpImage.info.absHeight;
        }

        RenderTexture renderTexture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);

        Texture2D textureEditable = new Texture2D(renderTexture.width, renderTexture.height);

        // don't forget that you need to specify rendertexture before you call readpixels otherwise it will read screen pixels.
        RenderTexture.active = renderTexture;
        textureEditable.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

        // write bmp pixels ************************** THIS CAN BE ANY PIXEL MANIUPATION HERE ************************

        if(transparent)
        {
            FixTransparentFringePixels(bmpImage);
        }

        for (int x = 0; x < renderTexture.width; x++)
        {
            for (int y = 0; y < renderTexture.height; y++)
            {
                int imageDataIndex;
                if (rotate)
                {
                    imageDataIndex = (bmpImage.info.absWidth * x) + y;
                }
                else
                {
                    imageDataIndex = (bmpImage.info.absWidth * y) + x;
                }

                Color32 color32 = bmpImage.imageData[imageDataIndex];
                Color32 maskColor32 = Color.white;
                if (bmpMaskImage != null)
                {
                    maskColor32 = bmpMaskImage.imageData[imageDataIndex];
                }

                float r = (float)color32.r / 255.0f;
                float g = (float)color32.g / 255.0f;
                float b = (float)color32.b / 255.0f;
                float a;
                if(transparent)
                {
                    a = (float)color32.a / 255.0f;

                    if(bmpMaskImage != null)
                    {
                        // TO IMPROVE - have very occasionally seen yellow tinted masks, whie I just want alpha, I might need 
                        // to read more than red channel to get a more accurate representation of intended transparency
                        float maskR = (float)maskColor32.r / 255.0f; // r is enough, white represents alpha on the mask image
                        if(a > 0f)
                        {
                            a = maskR;
                        }
                    }
                }
                else
                {
                    a = 1f;
                }
 
                int writeX = mirrorX ? renderTexture.width - x : x;
                int writeY = mirrorY ? renderTexture.height - y - 1 : y;

                textureEditable.SetPixel(writeX, writeY, new Color(r, g, b, a));
            }
        }

        textureEditable.Apply();

        byte[] bytes = textureEditable.EncodeToPNG();
        if(pngFilePathIsWithinAssets)
        {
            File.WriteAllBytes(Path.Combine(Application.dataPath, pngFilepath + pngFilename), bytes);
        }
        else
        {
            File.WriteAllBytes(pngFilepath + pngFilename, bytes);
        }

        RenderTexture.active = null; // don't forget to set it back to null once you finished playing with it. 
    }

    public static Vector2Int GetImageDataFromBmp(string bmpFilePath, string bmpMaskFilePath, out Color32[] imageData, bool alphaSupport)
    {
        BMPLoader bmpLoader = new BMPLoader();
        bmpLoader.ReadPaletteAlpha = alphaSupport;
        bmpLoader.ForceAlphaReadWhenPossible = alphaSupport;

        BMPImage bmpImage = bmpLoader.LoadBMP(bmpFilePath);
        BMPImage bmpMaskImage = null;
        if (bmpMaskFilePath != null)
        {
            bmpMaskImage = bmpLoader.LoadBMP(bmpMaskFilePath);

            //UnityEngine.Debug.LogError("bmpFilePath == " + bmpFilePath + "    bmpMaskFilePath == " + bmpMaskFilePath);
        }

        if (alphaSupport)
        {
            FixTransparentFringePixels(bmpImage);
        }

        imageData = bmpImage.imageData;

        int imageWidth = bmpImage.info.absWidth;
        int imageHeight = bmpImage.info.absHeight;
        int maskImageWidth = bmpMaskImage != null ? bmpMaskImage.info.absWidth : 0;
        int maskImageHeight = bmpMaskImage != null ? bmpMaskImage.info.absHeight : 0;

        for(int imageX = 0; imageX < imageWidth; ++imageX)
        {
            for (int imageY = 0; imageY < imageHeight; ++imageY)
            {
                int imageDataIndex = (imageY * imageWidth) + imageX;

                float imageXNormalised = (float)imageX / (imageWidth - 1);
                float imageYNormalised = (float)imageY / (imageHeight - 1);

                int maskXRelative = (int)Mathf.Lerp(0, maskImageWidth - 1, imageXNormalised);
                int maskYRelative = (int)Mathf.Lerp(0, maskImageHeight - 1, imageYNormalised);

                //int maskImageDataIndex = (imageY * maskImageWidth) + imageX;
                int maskImageDataIndex = (maskYRelative * maskImageWidth) + maskXRelative;

                if (alphaSupport) 
                {
                    if (bmpMaskImage != null)
                    {
                        // TOREMOVE - shouldn't need this check now doing mask coords as relative to size of 'owner' lamp image
                        //if (imageX >= maskImageWidth || imageY >= maskImageHeight)
                        //{
                        //    Debug.LogError("Gone off the horizontal/vertical edge of the bmpMaskImage - skipping");
                        //    imageData[imageDataIndex].a = 0;
                        //    continue;
                        //}

                        // applying both the alpha plsu the actual hue, since Dad is using red alpha masks to make red lamps on his layouts
                        byte maximumRGBValue = (byte)Mathf.Max(
                            bmpMaskImage.imageData[maskImageDataIndex].r, bmpMaskImage.imageData[maskImageDataIndex].g, bmpMaskImage.imageData[maskImageDataIndex].b);

                        byte maskAlpha = maximumRGBValue;
                        if (imageData[imageDataIndex].a > 0f)
                        {
                            imageData[imageDataIndex].a = maskAlpha;

                            // not sure how correct this is:
                            imageData[imageDataIndex].r = (byte)((imageData[imageDataIndex].r * bmpMaskImage.imageData[maskImageDataIndex].r) / 255.0f);
                            imageData[imageDataIndex].g = (byte)((imageData[imageDataIndex].g * bmpMaskImage.imageData[maskImageDataIndex].g) / 255.0f);
                            imageData[imageDataIndex].b = (byte)((imageData[imageDataIndex].b * bmpMaskImage.imageData[maskImageDataIndex].b) / 255.0f);
                        }
                    }
                }
                else
                {
                    imageData[imageDataIndex].a = 255;
                }
            }
        }

        Vector2Int size = Vector2Int.zero;
        size.x = bmpImage.info.absWidth;
        size.y = bmpImage.info.absHeight;

        return size;
    }

 
// ******* NO LONGER USED ******************
//    public static void WritePng(
//        Texture2D texture2d,
//        string pngFilepathWithinAssets,
//        string pngFilename)
//    {
//        RenderTexture renderTexture = new RenderTexture(texture2d.width, texture2d.height, 0, RenderTextureFormat.ARGB32);

//        Texture2D textureEditable = new Texture2D(renderTexture.width, renderTexture.height);

//        // don't forget that you need to specify rendertexture before you call readpixels otherwise it will read screen pixels.
//        RenderTexture.active = renderTexture;
//        textureEditable.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

//        // write bmp pixels ************************** THIS CAN BE ANY PIXEL MANIUPATION HERE ************************
//        textureEditable.SetPixels(texture2d.GetPixels());
//        //for (int x = 0; x < renderTexture.width; x++)
//        //{
//        //    for (int y = 0; y < renderTexture.height; y++)
//        //    {
//        //        Color32 color32 = imageData[(width * y) + x];

//        //        float r = (float)color32.r / 255.0f;
//        //        float g = (float)color32.g / 255.0f;
//        //        float b = (float)color32.b / 255.0f;
//        //        float a = (float)color32.a / 255.0f;

//        //        textureEditable.SetPixel(x, y, new Color(r, g, b));
//        //    }
//        //}

//        textureEditable.Apply();

//        byte[] bytes = textureEditable.EncodeToPNG();
//        File.WriteAllBytes(Path.Combine(Application.dataPath, pngFilepathWithinAssets + pngFilename), bytes);

//#if UNITY_EDITOR
//        AssetDatabase.ImportAsset("Assets/" + pngFilepathWithinAssets + pngFilename, ImportAssetOptions.ForceUpdate);
//        AssetDatabase.Refresh();

//        Destroy(textureEditable);
//#endif

//        RenderTexture.active = null; // don't forget to set it back to null once you finished playing with it. 
//    }

    private static void FixTransparentFringePixels(BMPImage bmpImage)
    {
        int width = bmpImage.info.absWidth;
        int height = bmpImage.info.absHeight;
        
        for(int x = 0; x < width; ++x)
        {
            for(int y = 0; y < height; ++y)
            {
                Color32 pixel = bmpImage.imageData[(width * y) + x];
                if(pixel.a == 255)
                {
                    continue;
                }

                Color32? pixelN = null;
                Color32? pixelNE = null;
                Color32? pixelE = null;
                Color32? pixelSE = null;
                Color32? pixelS = null;
                Color32? pixelSW = null;
                Color32? pixelW = null;
                Color32? pixelNW = null;

                // read neighbour pixels
                if (y > 0)
                {
                    pixelN = bmpImage.imageData[(width * (y - 1)) + x];
                }

                if (y > 0 && x < width - 1)
                {
                    pixelNE = bmpImage.imageData[(width * (y - 1)) + (x + 1)];
                }

                if (x < width - 1)
                {
                    pixelE = bmpImage.imageData[(width * y) + (x + 1)];
                }

                if (y < height - 1 && x < width - 1)
                {
                    pixelSE = bmpImage.imageData[(width * (y + 1)) + (x + 1)];
                }

                if (y < height - 1)
                {
                    pixelS = bmpImage.imageData[(width * (y + 1)) + x];
                }

                if (y < height - 1 && x > 0)
                {
                    pixelSW = bmpImage.imageData[(width * (y + 1)) + (x - 1)];
                }

                if (x > 0)
                {
                    pixelW = bmpImage.imageData[(width * y) + (x - 1)];
                }

                if (y > 0 && x > 0)
                {
                    pixelNW = bmpImage.imageData[(width * (y - 1)) + (x - 1)];
                }

                // find usable neighbour pixel (do nsew then diagonals as nsew are 'closer' to the pixel we are working on
                Color32? usableNeighbourPixel = null;
                if (pixelN.HasValue && pixelN.Value.a == 255)
                {
                    usableNeighbourPixel = pixelN.Value;
                }
                else if (pixelS.HasValue && pixelS.Value.a == 255)
                {
                    usableNeighbourPixel = pixelS.Value;
                }
                else if (pixelE.HasValue && pixelE.Value.a == 255)
                {
                    usableNeighbourPixel = pixelE.Value;
                }
                else if (pixelW.HasValue && pixelW.Value.a == 255)
                {
                    usableNeighbourPixel = pixelW.Value;
                }
                else if (pixelNE.HasValue && pixelNE.Value.a == 255)
                {
                    usableNeighbourPixel = pixelNE.Value;
                }
                else if (pixelSE.HasValue && pixelSE.Value.a == 255)
                {
                    usableNeighbourPixel = pixelSE.Value;
                }
                else if (pixelSW.HasValue && pixelSW.Value.a == 255)
                {
                    usableNeighbourPixel = pixelSW.Value;
                }
                else if (pixelNW.HasValue && pixelNW.Value.a == 255)
                {
                    usableNeighbourPixel = pixelNW.Value;
                }

                // update pixel if usable neighbour pixel found
                if(usableNeighbourPixel != null)
                {
                    bmpImage.imageData[(width * y) + x] = (Color32)usableNeighbourPixel;
                    bmpImage.imageData[(width * y) + x].a = 0; // correct back to fully transparent
                }
            }
        }
    }

}
