using MfmeTools.UnityStructWrappers;
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
        public IntPtr Handle = IntPtr.Zero;
        public RECT Rect = new RECT();
        public SharpDX.Direct3D11.Device Device = null;

        private readonly ICaptureMethod _captureMethod;

        public MfmeWindow(ICaptureMethod captureMethod)
        {
            _captureMethod = captureMethod;
        }

        public Color32[] GetPixels(int _x, int _y, int _width, int _height)
        {
            // TODO this should be just set up once, and needs Device and Factory objects
            _captureMethod.StartCapture(Handle, null, null);


            using var texture2d = _captureMethod.TryGetNextFrameAsTexture2D(null);


            int width = texture2d.Description.Width;
            int height = texture2d.Description.Height;

            Console.WriteLine($"Texture width: {width}, height {height}");



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

            using (Texture2D textureStaging = new Texture2D(Device, description))
            {
                Device.ImmediateContext.CopyResource(texture2d, textureStaging);

                DataStream dataStream;
                DataBox dataBox = Device.ImmediateContext.MapSubresource(textureStaging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None, out dataStream);

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
                Device.ImmediateContext.UnmapSubresource(textureStaging, 0);

                // JP second test, print some bitmap data values:
                // Assuming 'bitmapData' is your System.Drawing.Imaging.BitmapData object

                // Calculate the index of the pixel data
                const int kPixelSize = 4; // For Format32bppArgb, each pixel is represented by 4 bytes (ARGB)
                int yPosition = 200;
                int xPosition = 200;
                int index = yPosition * bitmapData.Stride + xPosition * kPixelSize;

                // Get the pointer to the pixel data
                IntPtr pixelDataPtr = bitmapData.Scan0;

                // Read the color bytes
                byte blue = Marshal.ReadByte(pixelDataPtr, index);
                byte green = Marshal.ReadByte(pixelDataPtr, index + 1);
                byte red = Marshal.ReadByte(pixelDataPtr, index + 2);
                byte alpha = Marshal.ReadByte(pixelDataPtr, index + 3);

                // Print the red byte value
                Console.WriteLine($"RGB byte values at " +
                    $"row {yPosition}, column {xPosition}: {red},{green},{blue}");
            }


            // TODO
            return null;
        }
    }
}
