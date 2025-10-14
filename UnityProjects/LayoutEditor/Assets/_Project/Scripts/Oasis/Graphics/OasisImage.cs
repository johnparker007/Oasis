using Oasis.Utility;
using System;
using System.IO;
//using OpenCVForUnity.CoreModule;
//using OpenCVForUnity.UtilsModule;
//using System.Drawing;
//using System.Drawing.Imaging;
//using System.Runtime.InteropServices;
using UnityEngine;

namespace Oasis.Graphics
{
    [Serializable]
    public class OasisImage
    {
        // we hide this as these arrays can be very large and will hang the editor
        [HideInInspector]
        public Color32[] ImageData;

        public int Width;
        public int Height;


        public OasisImage(int width, int height)
        {
            ImageData = new Color32[width * height];

            Width = width;
            Height = height;
        }

        public OasisImage(Vector2Int size)
        {
            ImageData = new Color32[size.x * size.y];

            Width = size.x;
            Height = size.y;
        }

        public OasisImage(Color32 fillColor, int width, int height)
        {
            ImageData = new Color32[width * height];

            for (int imageDataIndex = 0; imageDataIndex < ImageData.Length; ++imageDataIndex)
            {
                ImageData[imageDataIndex] = fillColor;
            }

            Width = width;
            Height = height;
        }

        public OasisImage(Color32[] imageData, int width, int height)
        {
            ImageData = new Color32[width * height];
            System.Array.Copy(imageData, ImageData, ImageData.Length);

            Width = width;
            Height = height;
        }

        public OasisImage(byte[] imageDataBytes, int width, int height)
        {
            ImageData = ByteArrayToColor32Array(imageDataBytes, true);

            Width = width;
            Height = height;
        }

        public OasisImage(string bmpFilePath, string bmpMaskFilePath, bool alphaSupport)
        {
            Vector2Int imageSize = BmpHelper.GetImageDataFromBmp(bmpFilePath, bmpMaskFilePath, out ImageData, alphaSupport);

            Width = imageSize.x;
            Height = imageSize.y;

            FlipY(); // we Y-Flip as the BMP comes in 'upside-down' - this may later want to find its way into BmpHelper.GetImageDataFromBmp
        }

        public OasisImage(byte[] pngFileBytes)
        {
            Texture2D texture2D = PngHelper.LoadPngToTexture2D(pngFileBytes);

            ImageData = texture2D.GetPixels32();

            Width = texture2D.width;
            Height = texture2D.height;
        }

        public OasisImage(Texture2D texture2D)
        {
            ImageData = texture2D.GetPixels32();

            Width = texture2D.width;
            Height = texture2D.height;
        }

        public OasisImage(OasisImage sourceOasisImage, RectInt cropRect)
        {
            OasisImage oasisImageCropped = new OasisImage(cropRect.width, cropRect.height);

            for (int writeX = 0; writeX < cropRect.width; ++writeX)
            {
                for (int writeY = 0; writeY < cropRect.height; ++writeY)
                {
                    Color32 sourcePixel = sourceOasisImage.ImageData
                        [(sourceOasisImage.Width * (writeY + cropRect.y)) + writeX + cropRect.x];

                    oasisImageCropped.ImageData[(cropRect.width * writeY) + writeX] = sourcePixel;
                }
            }

            InitialiseFromOasisImage(oasisImageCropped);
        }

        // this constructer creates a Difference converter image
        public OasisImage(OasisImage differenceSourceImageA, OasisImage differenceSourceImageB)
        {
            if (differenceSourceImageA.Width != differenceSourceImageB.Width
                || differenceSourceImageA.Height != differenceSourceImageB.Height)
            {
                Debug.LogError("Source images must be the same size!");
                return;
            }

            Width = differenceSourceImageA.Width;
            Height = differenceSourceImageA.Height;
            ImageData = new Color32[Width * Height];

            for (int imageDataIndex = 0; imageDataIndex < ImageData.Length; ++imageDataIndex)
            {
                Color32 sourcePixelA = differenceSourceImageA.ImageData[imageDataIndex];
                Color32 sourcePixelB = differenceSourceImageB.ImageData[imageDataIndex];

                ImageData[imageDataIndex].r = (byte)Mathf.Abs(sourcePixelA.r - sourcePixelB.r);
                ImageData[imageDataIndex].g = (byte)Mathf.Abs(sourcePixelA.g - sourcePixelB.g);
                ImageData[imageDataIndex].b = (byte)Mathf.Abs(sourcePixelA.b - sourcePixelB.b);
                ImageData[imageDataIndex].a = 1;
            }
        }

        //public ConverterImage(string binaryFormatPath)
        //{
        //    byte[] fileBytes = File.ReadAllBytes(binaryFormatPath);

        //    BinaryFormatter binaryFormatter = new BinaryFormatter();
        //    MemoryStream memoryStream = new MemoryStream(fileBytes);
        //    ConverterImage converterImage = (ConverterImage)binaryFormatter.Deserialize(memoryStream);
        //    memoryStream.Close();

        //    InitialiseFromConverterImage(converterImage);
        //}

        public OasisImage(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            OasisImage oasisImage = JsonUtility.FromJson<OasisImage>(json);

            InitialiseFromOasisImage(oasisImage);
        }

        // TODO OpenCV routines to rewrite:
        //public ConverterImage(Mat mat)
        //{
        //    byte[] matImageBytes = new byte[mat.cols() * mat.rows() * 4];
        //    MatUtils.copyFromMat(mat, matImageBytes);

        //    ConverterImage converterImage = new ConverterImage(matImageBytes, mat.cols(), mat.rows());
        //    InitialiseFromConverterImage(converterImage);
        //}

        //public ConverterImage(Mat mat, Mat matMask)
        //{
        //    byte[] matImageBytes = new byte[mat.cols() * mat.rows() * 4];
        //    MatUtils.copyFromMat(mat, matImageBytes);

        //    byte[] matMaskImageBytes = new byte[matMask.cols() * matMask.rows()];
        //    MatUtils.copyFromMat(matMask, matMaskImageBytes);

        //    for (int maskIndex = 0; maskIndex < matMask.cols() * matMask.rows(); ++maskIndex)
        //    {
        //        matImageBytes[(maskIndex * 4) + 3] = matMaskImageBytes[maskIndex];
        //    }

        //    ConverterImage converterImage = new ConverterImage(matImageBytes, mat.cols(), mat.rows());
        //    InitialiseFromConverterImage(converterImage);
        //}

        public void InitialiseFromOasisImage(OasisImage sourceOasisImage)
        {
            ImageData = new Color32[sourceOasisImage.Width * sourceOasisImage.Height];
            Array.Copy(sourceOasisImage.ImageData, ImageData, ImageData.Length);

            Width = sourceOasisImage.Width;
            Height = sourceOasisImage.Height;
        }

        public OasisImage Clone()
        {
            return new OasisImage(ImageData, Width, Height);
        }

        public byte[] GetAsByteArray()
        {
            return Color32ArrayToByteArray(ImageData);
        }

        public byte[] GetAsByteArray(int channelIndex)
        {
            return Color32ArrayToByteArray(ImageData, channelIndex);
        }

        // TODO OpenCV routines to rewrite:
        //public Mat GetOpenCVMatCopy()
        //{
        //    Mat mat = new Mat(Height, Width, CvType.CV_8UC4);
        //    MatUtils.copyToMat(GetAsByteArray(), mat);

        //    return mat;
        //}

        //public Mat GetOpenCVMatMaskCopy()
        //{
        //    // FF00FF purple, so green channel is on/off for purple and white image mask
        //    const int kGreenChannelIndex = 1; // (RGBA)

        //    byte[] greenChannelBytes = GetAsByteArray(kGreenChannelIndex);
        //    bool invert = false;
        //    if (invert)
        //    {
        //        for (int byteIndex = 0; byteIndex < greenChannelBytes.Length; ++byteIndex)
        //        {
        //            greenChannelBytes[byteIndex] = greenChannelBytes[byteIndex] == 0 ? (byte)255 : (byte)0;
        //        }
        //    }

        //    Mat mat = new Mat(Height, Width, CvType.CV_8UC1);
        //    MatUtils.copyToMat(greenChannelBytes, mat);

        //    return mat;
        //}

        public Texture2D GetTexture2dCopy()
        {
            Texture2D texture2D = new Texture2D(Width, Height);

            texture2D.SetPixels32(ImageData);

            texture2D.Apply();

            return texture2D;
        }

        private static byte[] Color32ArrayToByteArray(Color32[] color32Data)
        {
            const int kARGBChannelCount = 4;

            int length = kARGBChannelCount * color32Data.Length;
            byte[] bytes = new byte[length];

            for (int byteIndex = 0; byteIndex < bytes.Length; byteIndex += kARGBChannelCount)
            {
                bytes[byteIndex + 0] = color32Data[byteIndex / kARGBChannelCount].b;
                bytes[byteIndex + 1] = color32Data[byteIndex / kARGBChannelCount].g;
                bytes[byteIndex + 2] = color32Data[byteIndex / kARGBChannelCount].r;
                bytes[byteIndex + 3] = color32Data[byteIndex / kARGBChannelCount].a;
            }

            return bytes;
        }

        private static byte[] Color32ArrayToByteArray(Color32[] color32Data, int channelIndex)
        {
            byte[] bytes = new byte[color32Data.Length];

            for (int byteIndex = 0; byteIndex < bytes.Length; ++byteIndex)
            {
                switch (channelIndex)
                {
                    case 0:
                        bytes[byteIndex] = color32Data[byteIndex].r;
                        break;
                    case 1:
                        bytes[byteIndex] = color32Data[byteIndex].g;
                        break;
                    case 2:
                        bytes[byteIndex] = color32Data[byteIndex].b;
                        break;
                    case 3:
                        bytes[byteIndex] = color32Data[byteIndex].a;
                        break;
                }
            }

            return bytes;
        }

        public static Color32[] ByteArrayToColor32Array(byte[] bytes, bool rgbaWriteOrder)
        {
            Color32[] color32Data = new Color32[bytes.Length / 4];
            for (int i = 0; i < bytes.Length / 4; i++)
            {
                if (rgbaWriteOrder)
                {
                    color32Data[i] = new Color32(bytes[i * 4 + 2], bytes[i * 4 + 1], bytes[i * 4], bytes[i * 4 + 3]);
                }
                else
                {
                    color32Data[i] = new Color32(bytes[i * 4 + 0], bytes[i * 4 + 1], bytes[i * 4 + 2], bytes[i * 4 + 3]);
                }
            }

            return color32Data;
        }

        public static OasisImage Transform(
            OasisImage sourceImage,
            Vector2Int pointA,
            Vector2Int pointB,
            Vector2Int pointC,
            Vector2Int pointD,
            float targetAspectRatio)
        {
            if (sourceImage == null)
            {
                throw new ArgumentNullException(nameof(sourceImage));
            }

            if (targetAspectRatio <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetAspectRatio), "Aspect ratio must be greater than zero.");
            }

            Vector2 a = pointA;
            Vector2 b = pointB;
            Vector2 c = pointC;
            Vector2 d = pointD;

            float horizontalLengthTop = Vector2.Distance(a, b);
            float horizontalLengthBottom = Vector2.Distance(d, c);
            float verticalLengthLeft = Vector2.Distance(a, d);
            float verticalLengthRight = Vector2.Distance(b, c);

            float idealWidth = Mathf.Max(horizontalLengthTop, horizontalLengthBottom);
            float idealHeight = Mathf.Max(verticalLengthLeft, verticalLengthRight);

            if (idealWidth <= 0.0f)
            {
                idealWidth = 1.0f;
            }

            if (idealHeight <= 0.0f)
            {
                idealHeight = 1.0f;
            }

            float computedWidth = idealWidth;
            float computedHeight = computedWidth / targetAspectRatio;

            if (computedHeight < idealHeight)
            {
                computedHeight = idealHeight;
                computedWidth = computedHeight * targetAspectRatio;
            }

            int targetWidth = Mathf.Max(1, Mathf.CeilToInt(computedWidth));
            int targetHeight = Mathf.Max(1, Mathf.CeilToInt(computedHeight));

            OasisImage transformedImage = new OasisImage(targetWidth, targetHeight);

            Vector2[] sourcePoints =
            {
                a,
                b,
                c,
                d
            };

            Vector2[] destinationPoints =
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(targetWidth - 1.0f, 0.0f),
                new Vector2(targetWidth - 1.0f, targetHeight - 1.0f),
                new Vector2(0.0f, targetHeight - 1.0f)
            };

            double[,] homography = SolveHomography(sourcePoints, destinationPoints);
            double[,] homographyInverse = Invert3x3Matrix(homography);

            for (int y = 0; y < targetHeight; ++y)
            {
                for (int x = 0; x < targetWidth; ++x)
                {
                    double destinationX = x;
                    double destinationY = y;

                    double denominator =
                        (homographyInverse[2, 0] * destinationX) +
                        (homographyInverse[2, 1] * destinationY) +
                        homographyInverse[2, 2];

                    if (Math.Abs(denominator) < 1e-8)
                    {
                        transformedImage.ImageData[(targetWidth * y) + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    double sourceX =
                        ((homographyInverse[0, 0] * destinationX) + (homographyInverse[0, 1] * destinationY) + homographyInverse[0, 2]) /
                        denominator;

                    double sourceY =
                        ((homographyInverse[1, 0] * destinationX) + (homographyInverse[1, 1] * destinationY) + homographyInverse[1, 2]) /
                        denominator;

                    Color32 sampledColor = SampleBicubicClamped(sourceImage, (float)sourceX, (float)sourceY);

                    transformedImage.ImageData[(targetWidth * y) + x] = sampledColor;
                }
            }

            return transformedImage;
        }

        public void FlipY()
        {
            Color32[] workBuffer = new Color32[Width * Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int readY = Height - y - 1;
                    workBuffer[(Width * y) + x] = ImageData[(Width * readY) + x];
                }
            }

            System.Array.Copy(workBuffer, ImageData, ImageData.Length);
        }

        private static double[,] SolveHomography(Vector2[] sourcePoints, Vector2[] destinationPoints)
        {
            if (sourcePoints == null)
            {
                throw new ArgumentNullException(nameof(sourcePoints));
            }

            if (destinationPoints == null)
            {
                throw new ArgumentNullException(nameof(destinationPoints));
            }

            if (sourcePoints.Length != 4 || destinationPoints.Length != 4)
            {
                throw new ArgumentException("Perspective transform requires exactly four source and destination points.");
            }

            double[,] coefficients = new double[8, 8];
            double[] results = new double[8];

            for (int i = 0; i < 4; ++i)
            {
                double sourceX = sourcePoints[i].x;
                double sourceY = sourcePoints[i].y;
                double destinationX = destinationPoints[i].x;
                double destinationY = destinationPoints[i].y;

                int row = i * 2;

                coefficients[row, 0] = sourceX;
                coefficients[row, 1] = sourceY;
                coefficients[row, 2] = 1.0;
                coefficients[row, 3] = 0.0;
                coefficients[row, 4] = 0.0;
                coefficients[row, 5] = 0.0;
                coefficients[row, 6] = -destinationX * sourceX;
                coefficients[row, 7] = -destinationX * sourceY;
                results[row] = destinationX;

                coefficients[row + 1, 0] = 0.0;
                coefficients[row + 1, 1] = 0.0;
                coefficients[row + 1, 2] = 0.0;
                coefficients[row + 1, 3] = sourceX;
                coefficients[row + 1, 4] = sourceY;
                coefficients[row + 1, 5] = 1.0;
                coefficients[row + 1, 6] = -destinationY * sourceX;
                coefficients[row + 1, 7] = -destinationY * sourceY;
                results[row + 1] = destinationY;
            }

            double[] homographyCoefficients = SolveLinearSystem(coefficients, results);

            double[,] homographyMatrix = new double[3, 3];

            homographyMatrix[0, 0] = homographyCoefficients[0];
            homographyMatrix[0, 1] = homographyCoefficients[1];
            homographyMatrix[0, 2] = homographyCoefficients[2];
            homographyMatrix[1, 0] = homographyCoefficients[3];
            homographyMatrix[1, 1] = homographyCoefficients[4];
            homographyMatrix[1, 2] = homographyCoefficients[5];
            homographyMatrix[2, 0] = homographyCoefficients[6];
            homographyMatrix[2, 1] = homographyCoefficients[7];
            homographyMatrix[2, 2] = 1.0;

            return homographyMatrix;
        }

        private static double[] SolveLinearSystem(double[,] coefficients, double[] results)
        {
            int length = results.Length;
            double[,] augmentedMatrix = new double[length, length + 1];

            for (int row = 0; row < length; ++row)
            {
                for (int column = 0; column < length; ++column)
                {
                    augmentedMatrix[row, column] = coefficients[row, column];
                }

                augmentedMatrix[row, length] = results[row];
            }

            for (int pivot = 0; pivot < length; ++pivot)
            {
                int maxRow = pivot;
                double maxValue = Math.Abs(augmentedMatrix[pivot, pivot]);
                for (int row = pivot + 1; row < length; ++row)
                {
                    double value = Math.Abs(augmentedMatrix[row, pivot]);
                    if (value > maxValue)
                    {
                        maxValue = value;
                        maxRow = row;
                    }
                }

                if (Math.Abs(maxValue) < 1e-12)
                {
                    throw new InvalidOperationException("Cannot solve homography; the provided points are degenerate.");
                }

                if (maxRow != pivot)
                {
                    for (int column = pivot; column <= length; ++column)
                    {
                        double temp = augmentedMatrix[pivot, column];
                        augmentedMatrix[pivot, column] = augmentedMatrix[maxRow, column];
                        augmentedMatrix[maxRow, column] = temp;
                    }
                }

                double pivotValue = augmentedMatrix[pivot, pivot];
                double inversePivot = 1.0 / pivotValue;

                for (int column = pivot; column <= length; ++column)
                {
                    augmentedMatrix[pivot, column] *= inversePivot;
                }

                for (int row = 0; row < length; ++row)
                {
                    if (row == pivot)
                    {
                        continue;
                    }

                    double factor = augmentedMatrix[row, pivot];
                    if (Math.Abs(factor) < 1e-12)
                    {
                        continue;
                    }

                    for (int column = pivot; column <= length; ++column)
                    {
                        augmentedMatrix[row, column] -= factor * augmentedMatrix[pivot, column];
                    }
                }
            }

            double[] solution = new double[length];
            for (int row = 0; row < length; ++row)
            {
                solution[row] = augmentedMatrix[row, length];
            }

            return solution;
        }

        private static double[,] Invert3x3Matrix(double[,] matrix)
        {
            double determinant =
                (matrix[0, 0] * ((matrix[1, 1] * matrix[2, 2]) - (matrix[1, 2] * matrix[2, 1]))) -
                (matrix[0, 1] * ((matrix[1, 0] * matrix[2, 2]) - (matrix[1, 2] * matrix[2, 0]))) +
                (matrix[0, 2] * ((matrix[1, 0] * matrix[2, 1]) - (matrix[1, 1] * matrix[2, 0])));

            if (Math.Abs(determinant) < 1e-12)
            {
                throw new InvalidOperationException("Homography matrix is singular and cannot be inverted.");
            }

            double inverseDeterminant = 1.0 / determinant;

            double[,] inverse = new double[3, 3];

            inverse[0, 0] = ((matrix[1, 1] * matrix[2, 2]) - (matrix[1, 2] * matrix[2, 1])) * inverseDeterminant;
            inverse[0, 1] = ((matrix[0, 2] * matrix[2, 1]) - (matrix[0, 1] * matrix[2, 2])) * inverseDeterminant;
            inverse[0, 2] = ((matrix[0, 1] * matrix[1, 2]) - (matrix[0, 2] * matrix[1, 1])) * inverseDeterminant;
            inverse[1, 0] = ((matrix[1, 2] * matrix[2, 0]) - (matrix[1, 0] * matrix[2, 2])) * inverseDeterminant;
            inverse[1, 1] = ((matrix[0, 0] * matrix[2, 2]) - (matrix[0, 2] * matrix[2, 0])) * inverseDeterminant;
            inverse[1, 2] = ((matrix[0, 2] * matrix[1, 0]) - (matrix[0, 0] * matrix[1, 2])) * inverseDeterminant;
            inverse[2, 0] = ((matrix[1, 0] * matrix[2, 1]) - (matrix[1, 1] * matrix[2, 0])) * inverseDeterminant;
            inverse[2, 1] = ((matrix[0, 1] * matrix[2, 0]) - (matrix[0, 0] * matrix[2, 1])) * inverseDeterminant;
            inverse[2, 2] = ((matrix[0, 0] * matrix[1, 1]) - (matrix[0, 1] * matrix[1, 0])) * inverseDeterminant;

            return inverse;
        }

        private static Color GetPixelClamped(OasisImage sourceImage, int x, int y)
        {
            if (sourceImage == null)
            {
                throw new ArgumentNullException(nameof(sourceImage));
            }

            if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
            {
                return new Color(0.0f, 0.0f, 0.0f, 0.0f);
            }

            int clampedX = Mathf.Clamp(x, 0, sourceImage.Width - 1);
            int clampedY = Mathf.Clamp(y, 0, sourceImage.Height - 1);

            return sourceImage.ImageData[(sourceImage.Width * clampedY) + clampedX];
        }

        private static float CubicCatmullRom(float value0, float value1, float value2, float value3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f *
                ((2.0f * value1) +
                (-value0 + value2) * t +
                ((2.0f * value0) - (5.0f * value1) + (4.0f * value2) - value3) * t2 +
                (-value0 + (3.0f * value1) - (3.0f * value2) + value3) * t3);
        }

        private static Color CubicCatmullRom(Color value0, Color value1, Color value2, Color value3, float t)
        {
            return new Color(
                CubicCatmullRom(value0.r, value1.r, value2.r, value3.r, t),
                CubicCatmullRom(value0.g, value1.g, value2.g, value3.g, t),
                CubicCatmullRom(value0.b, value1.b, value2.b, value3.b, t),
                CubicCatmullRom(value0.a, value1.a, value2.a, value3.a, t));
        }

        private static Color32 SampleBicubicClamped(OasisImage sourceImage, float x, float y)
        {
            if (sourceImage == null)
            {
                throw new ArgumentNullException(nameof(sourceImage));
            }

            if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
            {
                return new Color32(0, 0, 0, 0);
            }

            if (float.IsNaN(x) || float.IsNaN(y))
            {
                return new Color32(0, 0, 0, 0);
            }

            if (sourceImage.Width < 2 || sourceImage.Height < 2)
            {
                return SampleBilinearClamped(sourceImage, x, y);
            }

            float clampedX = Mathf.Clamp(x, 0.0f, sourceImage.Width - 1.0f);
            float clampedY = Mathf.Clamp(y, 0.0f, sourceImage.Height - 1.0f);

            int x0 = Mathf.FloorToInt(clampedX);
            int y0 = Mathf.FloorToInt(clampedY);

            float tx = clampedX - x0;
            float ty = clampedY - y0;

            Color row0 = CubicCatmullRom(
                GetPixelClamped(sourceImage, x0 - 1, y0 - 1),
                GetPixelClamped(sourceImage, x0, y0 - 1),
                GetPixelClamped(sourceImage, x0 + 1, y0 - 1),
                GetPixelClamped(sourceImage, x0 + 2, y0 - 1),
                tx);

            Color row1 = CubicCatmullRom(
                GetPixelClamped(sourceImage, x0 - 1, y0),
                GetPixelClamped(sourceImage, x0, y0),
                GetPixelClamped(sourceImage, x0 + 1, y0),
                GetPixelClamped(sourceImage, x0 + 2, y0),
                tx);

            Color row2 = CubicCatmullRom(
                GetPixelClamped(sourceImage, x0 - 1, y0 + 1),
                GetPixelClamped(sourceImage, x0, y0 + 1),
                GetPixelClamped(sourceImage, x0 + 1, y0 + 1),
                GetPixelClamped(sourceImage, x0 + 2, y0 + 1),
                tx);

            Color row3 = CubicCatmullRom(
                GetPixelClamped(sourceImage, x0 - 1, y0 + 2),
                GetPixelClamped(sourceImage, x0, y0 + 2),
                GetPixelClamped(sourceImage, x0 + 1, y0 + 2),
                GetPixelClamped(sourceImage, x0 + 2, y0 + 2),
                tx);

            Color blended = CubicCatmullRom(row0, row1, row2, row3, ty);

            blended.r = Mathf.Clamp01(blended.r);
            blended.g = Mathf.Clamp01(blended.g);
            blended.b = Mathf.Clamp01(blended.b);
            blended.a = Mathf.Clamp01(blended.a);

            Color32 result = blended;
            return result;
        }

        private static Color32 SampleBilinearClamped(OasisImage sourceImage, float x, float y)
        {
            if (sourceImage == null)
            {
                throw new ArgumentNullException(nameof(sourceImage));
            }

            if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
            {
                return new Color32(0, 0, 0, 0);
            }

            if (float.IsNaN(x) || float.IsNaN(y))
            {
                return new Color32(0, 0, 0, 0);
            }

            float clampedX = Mathf.Clamp(x, 0.0f, sourceImage.Width - 1.0f);
            float clampedY = Mathf.Clamp(y, 0.0f, sourceImage.Height - 1.0f);

            int x0 = Mathf.FloorToInt(clampedX);
            int y0 = Mathf.FloorToInt(clampedY);
            int x1 = Mathf.Min(x0 + 1, sourceImage.Width - 1);
            int y1 = Mathf.Min(y0 + 1, sourceImage.Height - 1);

            float tx = clampedX - x0;
            float ty = clampedY - y0;

            Color bottomLeft = sourceImage.ImageData[(sourceImage.Width * y0) + x0];
            Color bottomRight = sourceImage.ImageData[(sourceImage.Width * y0) + x1];
            Color topLeft = sourceImage.ImageData[(sourceImage.Width * y1) + x0];
            Color topRight = sourceImage.ImageData[(sourceImage.Width * y1) + x1];

            Color bottom = Color.Lerp(bottomLeft, bottomRight, tx);
            Color top = Color.Lerp(topLeft, topRight, tx);
            Color blended = Color.Lerp(bottom, top, ty);

            Color32 result = blended;
            return result;
        }

        public void FlipX()
        {
            Color32[] workBuffer = new Color32[Width * Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int readX = Width - x - 1;
                    workBuffer[(Width * y) + x] = ImageData[(Width * y) + readX];
                }
            }

            System.Array.Copy(workBuffer, ImageData, ImageData.Length);
        }

        // TODO all rotate operations/directions may be useful later 
        public void RotateClockwise90()
        {
            FlipX();// TOIMPROVE - this pre-flip is inefficient, should sort out the single operation to do all in one in the loop below

            Color32[] workBuffer = new Color32[Width * Height];

            int targetWidth = Height;
            int targetHeight = Width;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int readIndex = (Width * y) + x;

                    workBuffer[(targetWidth * x) + y] = ImageData[readIndex];
                }
            }

            System.Array.Copy(workBuffer, ImageData, ImageData.Length);

            Height = targetHeight;
            Width = targetWidth;
        }

        public void Draw(OasisImage sourceImage, int destinationX, int destinationY)
        {
            Draw(sourceImage, 0, 0, sourceImage.Width, sourceImage.Height, destinationX, destinationY);
        }

        public void Draw(OasisImage sourceImage, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int destinationX, int destinationY)
        {
            for (int readX = 0; readX < sourceWidth; ++readX)
            {
                for (int readY = 0; readY < sourceHeight; ++readY)
                {
                    Color32 readPixel = sourceImage.ImageData[(sourceImage.Width * (readY + sourceY)) + readX + sourceX];

                    int writeX = destinationX + readX;
                    int writeY = destinationY + readY;

                    SetPixel(writeX, writeY, readPixel);
                }
            }
        }

        public void SetPixel(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                Debug.LogWarning("OasisImage: skipping set pixel as out of bounds");
                return;
            }

            int imageDataIndex = (Width * y) + x;

            if (imageDataIndex >= ImageData.Length)
            {
                Debug.LogError("OasisImage: issue with Width/Height set wrong?");
                return;
            }

            ImageData[imageDataIndex] = color;
        }

        public void SetPixelAlpha(int x, int y, byte alpha)
        {
            ImageData[(Width * y) + x].a = alpha;
        }

        public Color32 GetPixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                Debug.LogError("Coordinate out of range");
                return new Color32();
            }

            return ImageData[(Width * y) + x];
        }

        public void Clear(Color32 color)
        {
            for (int imageDataIndex = 0; imageDataIndex < ImageData.Length; ++imageDataIndex)
            {
                ImageData[imageDataIndex] = color;
            }
        }

        public void Crop(float xNormalised, float yNormalised, float widthNormalised, float heightNormalised)
        {
            Crop((int)(xNormalised * Width), (int)(yNormalised * Height), (int)(widthNormalised * Width), (int)(heightNormalised * Height));
        }

        public void Crop(int x, int y, int width, int height)
        {
            OasisImage croppedImage = new OasisImage(width, height);
            croppedImage.Draw(this, x, y, width, height, 0, 0);

            // copy cropped image to 'this'
            Width = width;
            Height = height;
            ImageData = croppedImage.ImageData;
        }

        // TODO may want to leave a 1-2 pixel gap arond edge for GPU sampling? Otherwise may get sharp line edges on curves at edge of texture when scaled
        public RectInt AutoCrop()
        {
            // TODO can optimise search speed by passing in found parameters so far to search thru less pixels

            int y1 = AutoCropGetTopRow();
            int y2 = AutoCropGetBottomRow();
            int x1 = AutoCropGetLeftColumn();
            int x2 = AutoCropGetRightColumn();
            int width = x2 - x1;
            int height = y2 - y1;

            //ConverterImage croppedImage = new ConverterImage(width, height);
            //croppedImage.Draw(this, x1, y1, width, height, 0, 0);

            //// copy cropped image to 'this'
            //Width = width;
            //Height = height;
            //ImageData = croppedImage.ImageData;
            Crop(x1, y1, width, height);

            return new RectInt(x1, y1, width, height);
        }

        private int AutoCropGetTopRow()
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    Color32 pixel = ImageData[(Width * y) + x];
                    if (pixel.a != 0)
                    {
                        return y;
                    }
                }
            }

            Debug.LogError("top row not found!");

            return 0;
        }

        private int AutoCropGetBottomRow()
        {
            for (int y = Height - 1; y >= 0; --y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    Color32 pixel = ImageData[(Width * y) + x];
                    if (pixel.a != 0)
                    {
                        return y;
                    }
                }
            }

            Debug.LogError("bottom row not found!");

            return 0;
        }

        private int AutoCropGetLeftColumn()
        {
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    Color32 pixel = ImageData[(Width * y) + x];
                    if (pixel.a != 0)
                    {
                        return x;
                    }
                }
            }

            Debug.LogError("left column not found!");

            return 0;
        }

        private int AutoCropGetRightColumn()
        {
            for (int x = Width - 1; x >= 0; --x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    Color32 pixel = ImageData[(Width * y) + x];
                    if (pixel.a != 0)
                    {
                        return x;
                    }
                }
            }

            Debug.LogError("right column not found!");

            return 0;
        }

        // for now, this will simply leave the previous pixels behind.
        // TOMIMPROVE: may want to scroll 1 px at a time, to leave trail when scrolling more than pixel in a direction
        // TOMIMPROVE: may want to have a wrap option
        public void ScrollPixels(int x, int y)
        {
            int sourceWidth = Width - Mathf.Abs(x);
            int sourceHeight = Height - Mathf.Abs(y);

            int sourceX = 0;
            int destinationX = Mathf.Abs(x);
            if (x < 0)
            {
                sourceX = Mathf.Abs(x);
                destinationX = 0;
            }

            int sourceY = 0;
            int destinationY = Mathf.Abs(y);
            if (y < 0)
            {
                sourceY = Mathf.Abs(y);
                destinationY = 0;
            }

            Draw(this, sourceX, sourceY, sourceWidth, sourceHeight, destinationX, destinationY);
        }

        public UnityEngine.Rect DetectAlphaRect(Vector2 normalisedApproximateCenter)
        {
            int approxCenterX = (int)(normalisedApproximateCenter.x * Width);
            int approxCenterY = (int)(normalisedApproximateCenter.y * Height);

            // TODO this will need more to get the 'maximum', this will just get the vertical and horizontal line from approx center extents...
            int y1 = DetectAlphaRectTop(approxCenterX, approxCenterY);
            int y2 = DetectAlphaRectBottom(approxCenterX, approxCenterY);
            int x1 = DetectAlphaRectLeft(approxCenterX, approxCenterY);
            int x2 = DetectAlphaRectRight(approxCenterX, approxCenterY);
            int width = x2 - x1;
            int height = y2 - y1;

            Debug.Log("Simple detect: x1 == " + x1 + "   y1 == " + y1 + "   x2 == " + x2 + "   y2 == " + y2 + "   width == " + width + "   height == " + height);

            int minimumDetectedLeftX = int.MaxValue;
            int maximumDetectedRightX = int.MinValue;
            for (int testRow = 0; testRow < height; ++testRow)
            {
                int testX = x1 + (int)(width * 0.5f);
                int testY = y1 + testRow;

                int detectedLeftX = DetectAlphaRectLeft(testX, testY);
                int detectedRightX = DetectAlphaRectRight(testX, testY);

                if (detectedLeftX < minimumDetectedLeftX)
                {
                    minimumDetectedLeftX = detectedLeftX;
                }

                if (detectedRightX > maximumDetectedRightX)
                {
                    maximumDetectedRightX = detectedRightX;
                }
            }

            x1 = minimumDetectedLeftX;
            x2 = maximumDetectedRightX;
            width = x2 - x1;

            int minimumDetectedTopY = int.MaxValue;
            int maximumDetectedBottomY = int.MinValue;
            for (int testColumn = 0; testColumn < width; ++testColumn)
            {
                int testX = x1 + testColumn;
                int testY = y1 + (int)(height * 0.5f);

                int detectedTopY = DetectAlphaRectTop(testX, testY);
                int detectedBottomY = DetectAlphaRectBottom(testX, testY);

                if (detectedTopY < minimumDetectedTopY)
                {
                    minimumDetectedTopY = detectedTopY;
                }

                if (detectedBottomY > maximumDetectedBottomY)
                {
                    maximumDetectedBottomY = detectedBottomY;
                }
            }

            y1 = minimumDetectedTopY;
            y2 = maximumDetectedBottomY;
            height = y2 - y1;

            Debug.Log("Second pass detect: x1 == " + x1 + "   y1 == " + y1 + "   x2 == " + x2 + "   y2 == " + y2 + "   width == " + width + "   height == " + height);

            UnityEngine.Rect alphaRectNormalised = new UnityEngine.Rect(
                (float)x1 / Width, (float)y1 / Height, (float)width / Width, (float)height / Height);

            return alphaRectNormalised;
        }

        private int DetectAlphaRectTop(int approxCenterX, int approxCenterY)
        {
            for (int y = approxCenterY; y >= 0; --y)
            {
                Color32 pixel = ImageData[(Width * y) + approxCenterX];
                if (pixel.a != 0)
                {
                    return y;
                }
            }

            Debug.LogError("DetectAlphaRectTop not found!");

            return 0;
        }

        private int DetectAlphaRectBottom(int approxCenterX, int approxCenterY)
        {
            for (int y = approxCenterY; y < Height; ++y)
            {
                Color32 pixel = ImageData[(Width * y) + approxCenterX];
                if (pixel.a != 0)
                {
                    return y;
                }
            }

            Debug.LogError("DetectAlphaRectBottom not found!");

            return 0;
        }

        private int DetectAlphaRectLeft(int approxCenterX, int approxCenterY)
        {
            for (int x = approxCenterX; x >= 0; --x)
            {
                Color32 pixel = ImageData[(Width * approxCenterY) + x];
                if (pixel.a != 0)
                {
                    return x;
                }
            }

            Debug.LogError("DetectAlphaRectLeft not found!");

            return 0;
        }

        private int DetectAlphaRectRight(int approxCenterX, int approxCenterY)
        {
            for (int x = approxCenterX; x < Width; ++x)
            {
                Color32 pixel = ImageData[(Width * approxCenterY) + x];
                if (pixel.a != 0)
                {
                    return x;
                }
            }

            Debug.LogError("DetectAlphaRectRight not found!");

            return 0;
        }

        // TODO Need to pull in MFMELampShape enum, but into something like an MFMEConstants class
        //public void ClearMFMELampShape(Converter.MFMELampShape mfmeLampShape)
        //{
        //    if (mfmeLampShape != Converter.MFMELampShape.Circle && mfmeLampShape != Converter.MFMELampShape.Ellipse)
        //    {
        //        return; // TODO only does circle/ellipse for now
        //    }

        //    float halfWidth = Width * 0.5f;
        //    float halfHeight = Height * 0.5f;

        //    // TODO shifting radius as we move across/down the image.  Quick workaround for now, just go inbetween the two 
        //    float radius = (halfWidth + halfHeight) * 0.5f;

        //    for (int x = 0; x < Width; ++x)
        //    {
        //        for (int y = 0; y < Height; ++y)
        //        {
        //            Vector2 originCentredPosition = Vector2.zero;
        //            originCentredPosition.x = -halfWidth + x;
        //            originCentredPosition.y = -halfHeight + y;

        //            if (originCentredPosition.magnitude > radius)
        //            {
        //                ImageData[(Width * y) + x].a = 0;
        //            }
        //        }
        //    }
        //}

        public byte[] GetAsPngBytes(TextureFormat textureFormat = TextureFormat.BGRA32)
        {
            Color[] scrapedPixelBlockColor = new Color[ImageData.Length];
            for (int i = 0; i < ImageData.Length; ++i)
            {
                scrapedPixelBlockColor[i] = ImageData[i];
            }

            Texture2D _testTexture = new Texture2D(Width, Height, textureFormat, false);

            _testTexture.SetPixels(scrapedPixelBlockColor);
            _testTexture.Apply();

            byte[] bytes = _testTexture.EncodeToPNG();

            GameObject.DestroyImmediate(_testTexture);

            return bytes;
        }

        //public void SaveToBinaryFormat(string path)
        //{
        //    BinaryFormatter binaryFormatter = new BinaryFormatter();
        //    FileStream fileStream = File.Open(path, FileMode.Create);
        //    binaryFormatter.Serialize(fileStream, this);
        //    fileStream.Close();
        //}

        public void SaveToJSONFormat(string path)
        {
            string json = JsonUtility.ToJson(this);
            File.WriteAllText(path, json);
        }

        public bool IsMatch(OasisImage comparisonImage, bool ignoreAlpha)
        {
            if (comparisonImage.Width != Width || comparisonImage.Height != Height)
            {
                Debug.LogError("Can't compare differently sized images!");
                return false;
            }

            for (int imageDataIndex = 0; imageDataIndex < ImageData.Length; ++imageDataIndex)
            {
                if (comparisonImage.ImageData[imageDataIndex].r != ImageData[imageDataIndex].r
                    || comparisonImage.ImageData[imageDataIndex].g != ImageData[imageDataIndex].g
                    || comparisonImage.ImageData[imageDataIndex].b != ImageData[imageDataIndex].b
                    || (!ignoreAlpha && comparisonImage.ImageData[imageDataIndex].a != ImageData[imageDataIndex].a))
                {
                    return false;
                }
            }

            return true;
        }

        // for working with a Difference image
        public int GetPixelCountAboveAverageValue(byte averageValue,
            float xNormalised, float yNormalised, float widthNormalised, float heightNormalised)
        {
            int scanRectX = (int)(Width * xNormalised);
            int scanRectY = (int)(Height * yNormalised);
            int scanRectWidth = (int)(Width * widthNormalised);
            int scanRectHeight = (int)(Height * heightNormalised);

            int pixelCountAboveAverage = 0;
            for (int readX = scanRectX; readX < scanRectX + scanRectWidth; ++readX)
            {
                for (int readY = scanRectY; readY < scanRectY + scanRectHeight; ++readY)
                {
                    Color32 pixel = ImageData[(Width * readY) + readX];

                    int pixelAverage = (pixel.r + pixel.g + pixel.b) / 3; // 3 is RGB channel count
                    if (pixelAverage > averageValue)
                    {
                        ++pixelCountAboveAverage;
                    }
                }
            }

            return pixelCountAboveAverage;
        }


    }
}