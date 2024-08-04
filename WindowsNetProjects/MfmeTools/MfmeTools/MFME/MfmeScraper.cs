using Oasis.MfmeTools.Shared.UnityWrappers;
using Oasis.MfmeTools.WindowCapture;
using Oasis.MfmeTools.WindowCapture.BitBlt;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Windows.Forms;
using static Oasis.MfmeTools.Shared.Mfme.MFMEConstants;
using Device = SharpDX.Direct3D11.Device;

namespace Oasis.MfmeTools.Mfme
{
    public static class MfmeScraper
    {
        public static readonly int kMfmeWindowTitlebarHeight = 30;

        public static readonly Color32 kMFMEImageBoxYellow = new Color32(255, 255, 0, 255);
        public static readonly Color32 kMFMEImageBoxWhite = new Color32(255, 255, 255, 255);
        public static readonly Color32 kMFMEImageBoxGrey = new Color32(192, 192, 192, 255);


        public static MfmeWindow SplashScreen = new MfmeWindow(new BitBlt());
        public static MfmeWindow MainForm = new MfmeWindow(new BitBlt());
        public static MfmeWindow Properties = new MfmeWindow(new BitBlt());
        public static MfmeWindow PropertiesFont = new MfmeWindow(new BitBlt());

        public static MfmeWindow CurrentWindow = null;

        // JP not sure if these need to be member variable yet:
        public static Device Device = null;
        public static SwapChain SwapChain = null;
        public static Factory Factory = null;

        public static void Initialise()
        {
            // JP not looking to actually output the image, can a dummy Form object work?
            Form form = new Form();

            // create a Device and SwapChain
            var swapChainDescription = new SwapChainDescription
            {
                BufferCount = 2,
                Flags = SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new ModeDescription(form.ClientSize.Width, form.ClientSize.Height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                OutputHandle = form.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, swapChainDescription, out Device, out SwapChain);

            using var swapChain1 = SwapChain.QueryInterface<SwapChain1>();

            // ignore all Windows events
            using var Factory = swapChain1.GetParent<Factory>();
            Factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAll);


            // JP not sure if should set up all windows here:
            //Properties.StartCapture();
        }

        public static bool GetCheckboxValue(int topLeftInteriorPixelX, int topLeftInteriorPixelY)
        {
            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 3;
            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 7;

            Color32 scrapedPixelColor32 = CurrentWindow.GetPixel(
                    topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
                    topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

            return scrapedPixelColor32.r != 255;
        }

        public static bool GetCheckboxPropertiesResultPanePreviewValue(int topLeftInteriorPixelX, int topLeftInteriorPixelY)
        {
// OASIS STILL TODO/TEST
            // these aren't quite right, need to understand why they work!
            //const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 9;
            //const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 6;
            //const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 2;
            //const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 4;
            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 3;
const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 7;

            Color32 scrapedPixelColor32 = CurrentWindow.GetPixel(
                    topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
                    topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

            return scrapedPixelColor32.r == 0;
        }

        public static Color32 GetColorboxValue(int topLeftInteriorPixelX, int topLeftInteriorPixelY)
        {
            const int kColorBoxInteriorSize = 19; // 19x19 pixels square
            const int kColorBoxCenterOffset = kColorBoxInteriorSize / 2;

            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = kColorBoxCenterOffset;
            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = kColorBoxCenterOffset;

            Color32 scrapedPixelColor32 =
                CurrentWindow.GetPixel(
                    topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
                    topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

            return scrapedPixelColor32;
        }

        public static bool GetRadioButtonValue(int topLeftInteriorPixelX, int topLeftInteriorPixelY)
        {
            const int kRadioButtonSize = 13; // 13x13 pixels square
            const int kRadioButtonCenterOffset = kRadioButtonSize / 2;

            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = kRadioButtonCenterOffset;
            const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = kRadioButtonCenterOffset;

            Color32 scrapedPixelColor32 =
                CurrentWindow.GetPixel(
                    topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
                    topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

            return scrapedPixelColor32.r < 127;
        }

        public static bool IsImageBoxBlank(
            int topLeftInteriorPixelX, int topLeftInteriorPixelY, int width, int height, bool checkerboard, bool blankIsYellow = true)
        {
// OASIS STILL TODO/TEST
            // I don't know why these all keep coming out differently?
            //const int kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsX = 8;
            //const int kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsY = 1;
const int kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsX = 0;
const int kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsY = 0;

            int readX = topLeftInteriorPixelX + kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsX;
            int readY = topLeftInteriorPixelY + kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsY;

            Color32[] imageBoxPixels = CurrentWindow.GetPixels(readX, readY, width, height);

            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    //Color32 scrapedPixelColor32 = CurrentWindow.GetPixel(readX + x, readY + y);
                    Color32 scrapedPixelColor32 = imageBoxPixels[(y * width) + x];
                    if (checkerboard)
                    {
                        if ((scrapedPixelColor32.r != kMFMEImageBoxWhite.r && scrapedPixelColor32.r != kMFMEImageBoxGrey.r)
                            || (scrapedPixelColor32.g != kMFMEImageBoxWhite.g && scrapedPixelColor32.g != kMFMEImageBoxGrey.g)
                            || (scrapedPixelColor32.b != kMFMEImageBoxWhite.b && scrapedPixelColor32.b != kMFMEImageBoxGrey.b))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Color32 blankColor = blankIsYellow ? kMFMEImageBoxYellow : kMFMEImageBoxWhite;

                        if (scrapedPixelColor32.r != blankColor.r
                            || scrapedPixelColor32.g != blankColor.g
                            || scrapedPixelColor32.b != blankColor.b)
                        {
                            return false;
                        }
                    }

                    // useful for debugging sraping:
                    //Console.WriteLine("x,y == " + x + "," + y + " == " + scrapedPixelColor32.r);
                }
            }

            return true;
        }

        public static KeyCode GetKeyCode(string scrapedCharacters)
        {
            string scrapedCharactersTrimmed = scrapedCharacters.TrimEnd(' ');
            switch (scrapedCharactersTrimmed)
            {
                // TODO worth doing escape / f0 - f9, insert, delete?

                case "SPACE":
                    return KeyCode.Space;
                case "1":
                    return KeyCode.Alpha1;
                case "2":
                    return KeyCode.Alpha2;
                case "3":
                    return KeyCode.Alpha3;
                case "4":
                    return KeyCode.Alpha4;
                case "5":
                    return KeyCode.Alpha5;
                case "6":
                    return KeyCode.Alpha6;
                case "7":
                    return KeyCode.Alpha7;
                case "8":
                    return KeyCode.Alpha8;
                case "9":
                    return KeyCode.Alpha9;
                case "0":
                    return KeyCode.Alpha0;
                case "`":
                    return KeyCode.BackQuote;
                case "-":
                    return KeyCode.Minus;
                case "=":
                    return KeyCode.Equals;
                case "A":
                    return KeyCode.A;
                case "B":
                    return KeyCode.B;
                case "C":
                    return KeyCode.C;
                case "D":
                    return KeyCode.D;
                case "E":
                    return KeyCode.E;
                case "F":
                    return KeyCode.F;
                case "G":
                    return KeyCode.G;
                case "H":
                    return KeyCode.H;
                case "I":
                    return KeyCode.I;
                case "J":
                    return KeyCode.J;
                case "K":
                    return KeyCode.K;
                case "L":
                    return KeyCode.L;
                case "M":
                    return KeyCode.M;
                case "N":
                    return KeyCode.N;
                case "O":
                    return KeyCode.O;
                case "P":
                    return KeyCode.P;
                case "Q":
                    return KeyCode.Q;
                case "R":
                    return KeyCode.R;
                case "S":
                    return KeyCode.S;
                case "T":
                    return KeyCode.T;
                case "U":
                    return KeyCode.U;
                case "V":
                    return KeyCode.V;
                case "W":
                    return KeyCode.W;
                case "X":
                    return KeyCode.X;
                case "Y":
                    return KeyCode.Y;
                case "Z":
                    return KeyCode.Z;
                case "[":
                    return KeyCode.LeftBracket;
                case "]":
                    return KeyCode.RightBracket;
                case ";":
                    return KeyCode.Semicolon;
                case "'":
                    return KeyCode.Quote;
                case "#":
                    return KeyCode.Hash;
                case "SHIFT":
                    return KeyCode.LeftShift;
                case "\\":
                    return KeyCode.Backslash;
                case ",":
                    return KeyCode.Comma;
                case ".":
                    return KeyCode.Period;
                case "/":
                    return KeyCode.Slash;
                case "CTRL":
                    return KeyCode.LeftControl;
                case "ALT":
                    return KeyCode.LeftAlt;
                case "UP":
                    return KeyCode.UpArrow;
                case "DOWN":
                    return KeyCode.DownArrow;
                case "LEFT":
                    return KeyCode.LeftArrow;
                case "RIGHT":
                    return KeyCode.RightArrow;
                default:
                    return KeyCode.None;
            }
        }

        public static WindowsInput.Native.VirtualKeyCode GetVirtualKeyCode(KeyCode unityKeycode)
        {
            // JP: these are UK codes to work with my keyboard
            switch (unityKeycode)
            {
                case KeyCode.Space:
                    return WindowsInput.Native.VirtualKeyCode.SPACE;
                case KeyCode.Alpha1:
                    return WindowsInput.Native.VirtualKeyCode.VK_1;
                case KeyCode.Alpha2:
                    return WindowsInput.Native.VirtualKeyCode.VK_2;
                case KeyCode.Alpha3:
                    return WindowsInput.Native.VirtualKeyCode.VK_3;
                case KeyCode.Alpha4:
                    return WindowsInput.Native.VirtualKeyCode.VK_4;
                case KeyCode.Alpha5:
                    return WindowsInput.Native.VirtualKeyCode.VK_5;
                case KeyCode.Alpha6:
                    return WindowsInput.Native.VirtualKeyCode.VK_6;
                case KeyCode.Alpha7:
                    return WindowsInput.Native.VirtualKeyCode.VK_7;
                case KeyCode.Alpha8:
                    return WindowsInput.Native.VirtualKeyCode.VK_8;
                case KeyCode.Alpha9:
                    return WindowsInput.Native.VirtualKeyCode.VK_9;
                case KeyCode.Alpha0:
                    return WindowsInput.Native.VirtualKeyCode.VK_0;
                case KeyCode.BackQuote:
                    return WindowsInput.Native.VirtualKeyCode.OEM_3;
                case KeyCode.Minus:
                    return WindowsInput.Native.VirtualKeyCode.OEM_MINUS;
                case KeyCode.Equals:
                    return WindowsInput.Native.VirtualKeyCode.OEM_PLUS;
                case KeyCode.A:
                    return WindowsInput.Native.VirtualKeyCode.VK_A;
                case KeyCode.B:
                    return WindowsInput.Native.VirtualKeyCode.VK_B;
                case KeyCode.C:
                    return WindowsInput.Native.VirtualKeyCode.VK_C;
                case KeyCode.D:
                    return WindowsInput.Native.VirtualKeyCode.VK_D;
                case KeyCode.E:
                    return WindowsInput.Native.VirtualKeyCode.VK_E;
                case KeyCode.F:
                    return WindowsInput.Native.VirtualKeyCode.VK_F;
                case KeyCode.G:
                    return WindowsInput.Native.VirtualKeyCode.VK_G;
                case KeyCode.H:
                    return WindowsInput.Native.VirtualKeyCode.VK_H;
                case KeyCode.I:
                    return WindowsInput.Native.VirtualKeyCode.VK_I;
                case KeyCode.J:
                    return WindowsInput.Native.VirtualKeyCode.VK_J;
                case KeyCode.K:
                    return WindowsInput.Native.VirtualKeyCode.VK_K;
                case KeyCode.L:
                    return WindowsInput.Native.VirtualKeyCode.VK_L;
                case KeyCode.M:
                    return WindowsInput.Native.VirtualKeyCode.VK_M;
                case KeyCode.N:
                    return WindowsInput.Native.VirtualKeyCode.VK_N;
                case KeyCode.O:
                    return WindowsInput.Native.VirtualKeyCode.VK_O;
                case KeyCode.P:
                    return WindowsInput.Native.VirtualKeyCode.VK_P;
                case KeyCode.Q:
                    return WindowsInput.Native.VirtualKeyCode.VK_Q;
                case KeyCode.R:
                    return WindowsInput.Native.VirtualKeyCode.VK_R;
                case KeyCode.S:
                    return WindowsInput.Native.VirtualKeyCode.VK_S;
                case KeyCode.T:
                    return WindowsInput.Native.VirtualKeyCode.VK_T;
                case KeyCode.U:
                    return WindowsInput.Native.VirtualKeyCode.VK_U;
                case KeyCode.V:
                    return WindowsInput.Native.VirtualKeyCode.VK_V;
                case KeyCode.W:
                    return WindowsInput.Native.VirtualKeyCode.VK_W;
                case KeyCode.X:
                    return WindowsInput.Native.VirtualKeyCode.VK_X;
                case KeyCode.Y:
                    return WindowsInput.Native.VirtualKeyCode.VK_Y;
                case KeyCode.Z:
                    return WindowsInput.Native.VirtualKeyCode.VK_Z;
                case KeyCode.LeftBracket:
                    return WindowsInput.Native.VirtualKeyCode.OEM_4;
                case KeyCode.RightBracket:
                    return WindowsInput.Native.VirtualKeyCode.OEM_6;
                case KeyCode.Semicolon:
                    return WindowsInput.Native.VirtualKeyCode.OEM_1;
                case KeyCode.Quote:
                    return WindowsInput.Native.VirtualKeyCode.OEM_3;
                case KeyCode.Hash:
                    return WindowsInput.Native.VirtualKeyCode.OEM_7;
                case KeyCode.LeftShift:
                    return WindowsInput.Native.VirtualKeyCode.SHIFT;
                case KeyCode.Backslash:
                    return WindowsInput.Native.VirtualKeyCode.OEM_5;
                case KeyCode.Comma:
                    return WindowsInput.Native.VirtualKeyCode.OEM_COMMA;
                case KeyCode.Period:
                    return WindowsInput.Native.VirtualKeyCode.OEM_PERIOD;
                case KeyCode.Slash:
                    return WindowsInput.Native.VirtualKeyCode.OEM_2;
                case KeyCode.LeftControl:
                    return WindowsInput.Native.VirtualKeyCode.CONTROL;
                case KeyCode.LeftAlt:
                    return WindowsInput.Native.VirtualKeyCode.MENU;
                case KeyCode.UpArrow:
                    return WindowsInput.Native.VirtualKeyCode.UP;
                case KeyCode.DownArrow:
                    return WindowsInput.Native.VirtualKeyCode.DOWN;
                case KeyCode.LeftArrow:
                    return WindowsInput.Native.VirtualKeyCode.LEFT;
                case KeyCode.RightArrow:
                    return WindowsInput.Native.VirtualKeyCode.RIGHT;
                case KeyCode.None:
                    return WindowsInput.Native.VirtualKeyCode.NONAME; // to be ignored by my logic
                default:
                    OutputLog.LogError("Shouldn't get here! Undefined unity key code");
                    return WindowsInput.Native.VirtualKeyCode.NONAME; // to be ignored by my logic
            }
        }

        public static MFMELampShape GetLampShape(string scrapedCharacters)
        {
            string scrapedCharactersTrimmed = scrapedCharacters.TrimEnd(' ');

            switch (scrapedCharactersTrimmed)
            {
                case "Rectangle":
                case "Square":
                case "Rect Round":
                case "Square Round":
                case "Diamond":
                case "Star":
                case "Polygon":
                case "Triangle Left":
                case "Triangle Right":
                case "Triangle Up":
                case "Triangle Down":
                case "Semicircle Left":
                case "Semicircle Right":
                case "Semicircle Up":
                case "Semicircle Down":
                case "Pie":
                    return MFMELampShape.Rectangle;

                case "Ellipse":
                case "Circle":
                    return MFMELampShape.Circle;

                default:
                    OutputLog.LogError("Couldn't match lamp shape text: " + scrapedCharactersTrimmed);
                    return MFMELampShape.Rectangle;
            }


        }
    }
}
