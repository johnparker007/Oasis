using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsInput;
using System.Threading;


namespace MfmeTools
{
    public class Extractor
    {
        public struct Options
        {
            public string SourceLayoutPath;
            public bool UseCachedLampImages;
            public bool UseCachedReelImages;
            public bool ScrapeLamps5To8;
            public bool ScrapeLamps9To12;
        }

        public void StartExtraction(Options options)
        {
            OutputLog.Log("Starting Extraction");
            OutputLog.Log("Extraction source layout: " + options.SourceLayoutPath);

            Program.LayoutCopier.CopyToMfmeTools(options.SourceLayoutPath);
            OutputLog.Log("Copied source layout to MFME Tools");


            // XXX TEST
            //StartCoroutine
            InputSimulator inputSimulator = new InputSimulator();

            ExtractorCoroutine(inputSimulator);
        }

        private void ExtractorCoroutine(InputSimulator inputSimulator)
        {
            OutputLog.LogError("JP TEST Sending Win+M minimise keystroke combo");
//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LWIN, WindowsInput.Native.VirtualKeyCode.VK_M);

            Thread.Sleep(5000);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_J);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_O);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_H);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_N);
            //Thread.Sleep(50);

            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LWIN, WindowsInput.Native.VirtualKeyCode.VK_M);
            Thread.Sleep(50);


            //yield return null;
        }
    }
}
