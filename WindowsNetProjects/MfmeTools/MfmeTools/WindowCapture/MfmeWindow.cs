using MfmeTools.Mfme;
using MfmeTools.UnityWrappers;
using MfmeTools.WindowCapture.Shared.Interfaces;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static MfmeTools.WindowCapture.Shared.Interop.NativeMethods;

namespace MfmeTools.WindowCapture
{
    public class MfmeWindow
    {
        private const bool kDebutOutputGetPixels = true;

        public IntPtr Handle = IntPtr.Zero;
        public RECT Rect = new RECT();

        private readonly ICaptureMethod _captureMethod = null;
        private Color32[] _capturePixelData = null;

        private int _width = 0;
        private int _height = 0;

        public MfmeWindow(ICaptureMethod captureMethod)
        {
            _captureMethod = captureMethod;
        }

        public void StartCapture()
        {
            _captureMethod.StartCapture(Handle, MfmeScraper.Device, MfmeScraper.Factory);
        }

        public void UpdateCapture()
        {
            using var texture2d = _captureMethod.TryGetNextFrameAsTexture2D(MfmeScraper.Device);

            _width = texture2d.Description.Width;
            _height = texture2d.Description.Height;

            Console.WriteLine($"Texture width: {_width}, height {_height}");

            // Assuming 'device' is your SharpDX.Direct3D11.Device object
            // and 'texture2D' is your SharpDX.Direct3D11.Texture2D object

            Texture2DDescription description = new Texture2DDescription()
            {
                Width = texture2d.Description.Width,
                Height = texture2d.Description.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = texture2d.Description.Format,
                Usage = ResourceUsage.Staging,
                SampleDescription = new SampleDescription(1, 0),
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None
            };

            using (Texture2D textureStaging = new Texture2D(MfmeScraper.Device, description))
            {
                MfmeScraper.Device.ImmediateContext.CopyResource(texture2d, textureStaging);

                DataStream dataStream;
                DataBox dataBox = MfmeScraper.Device.ImmediateContext.MapSubresource(textureStaging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None, out dataStream);

                // Create a bitmap to store the pixel data
                Bitmap bitmap = new Bitmap(description.Width, description.Height, PixelFormat.Format32bppArgb);

                // Lock the bitmap's bits
                BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

                // Copy the data from the dataStream to the bitmap
                for (int y = 0; y < description.Height; y++)
                {
                    int offset = y * bitmapData.Stride;
                    dataStream.Position = y * dataBox.RowPitch;
                    byte[] rowData = new byte[bitmapData.Stride];
                    dataStream.Read(rowData, 0, bitmapData.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(rowData, 0, bitmapData.Scan0 + offset, bitmapData.Stride);
                }

                // Unlock the bitmap's bits
                bitmap.UnlockBits(bitmapData);

                // Unmap the resource when you're done
                MfmeScraper.Device.ImmediateContext.UnmapSubresource(textureStaging, 0);


                // JP create/populate a Color32[] array from the Marshalled byte data
                if (_capturePixelData == null)
                {
                    _capturePixelData = new Color32[_width * _height];
                }

                const int kPixelSize = 4; // For Format32bppArgb, each pixel is represented by 4 bytes (ARGB)
                for (int readX = 0; readX < _width; ++readX)
                {
                    for (int readY = 0; readY < _height; ++readY)
                    {
                        int readIndex = readY * bitmapData.Stride + readX * kPixelSize;

                        // Get the pointer to the pixel data
                        IntPtr pixelDataPtr = bitmapData.Scan0;

                        // Read the color bytes
                        byte blue = Marshal.ReadByte(pixelDataPtr, readIndex);
                        byte green = Marshal.ReadByte(pixelDataPtr, readIndex + 1);
                        byte red = Marshal.ReadByte(pixelDataPtr, readIndex + 2);
                        byte alpha = Marshal.ReadByte(pixelDataPtr, readIndex + 3);

                        int writeIndex = (readY * _width) + readX;
                        _capturePixelData[writeIndex].r = red;
                        _capturePixelData[writeIndex].g = green;
                        _capturePixelData[writeIndex].b = blue;
                        _capturePixelData[writeIndex].a = alpha;
                    }
                }
            }
        }

        public Color32[] GetPixels(int x, int y, int width, int height)
        {
            // JP do this offset for now, since the old ARcadeSim coordinates
            // were all created including the window titlebar
            y -= MfmeScraper.kMfmeWindowTitlebarHeight;

            if (x < 0 || y < 0 || x + width > _width || y + height > _height)
            {
                Console.WriteLine($"ERROR invalid read area" +
                    $" x: {x}, y: {y}. width: {width}, height: {height}");
                return null;
            }

            Color32[] pixelData = new Color32[width * height];

            int writeIndex = 0;
            for (int readY = y; readY < y + height; ++readY)
            {
                for (int readX = x; readX < x + width; ++readX)
                {
                    int readIndex = (readY * _width) + readX;
                    pixelData[writeIndex] = _capturePixelData[readIndex];
                    ++writeIndex;
                }
            }

            if(kDebutOutputGetPixels)
            {
                Console.WriteLine("--START GETPIXELS OUTPUT:");
                for (int pixelDataY = 0; pixelDataY < height; ++pixelDataY)
                {
                    string outputRow = "";
                    for (int pixelDataX = 0; pixelDataX < width; ++pixelDataX)
                    {
                        int readIndex = (pixelDataY * width) + pixelDataX;
                        Color32 pixel = pixelData[readIndex];
                        outputRow += pixel.r < 128 ? "1" : "0";
                    }
                    Console.WriteLine(outputRow);
                }
                Console.WriteLine("--END GETPIXELS OUTPUT:");
            }

            return pixelData;
        }

        public Color32 GetPixel(int x, int y)
        {
            // JP do this offset for now, since the old ARcadeSim coordinates
            // were all created including the window titlebar
            y -= MfmeScraper.kMfmeWindowTitlebarHeight;

            if (x < 0 || y < 0 || x  >= _width || y >= _height)
            {
                Console.WriteLine($"ERROR invalid read area" +
                    $" x: {x}, y: {y}");
                return new Color32(); ;
            }

            Color32 pixelData = _capturePixelData[(y * _width) + x];

            return pixelData;
        }
    }
}
