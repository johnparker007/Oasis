using MfmeTools.UnityWrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools.Extensions
{
    public static class BitmapExtensions
    {
        public static Color32[] GetImageData(this Bitmap bitmap)
        {
            Color32[] imageData = new Color32[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; ++y)
            {
                for (int x = 0; x < bitmap.Width; ++x)
                {
                    imageData[(y * bitmap.Width) + x] = new Color32(bitmap.GetPixel(x, y));
                }
            }

            return imageData;
        }
    }
}
