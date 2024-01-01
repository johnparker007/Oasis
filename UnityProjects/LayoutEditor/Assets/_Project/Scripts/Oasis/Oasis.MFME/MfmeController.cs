using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oasis.MFME
{
    public class MfmeController : MonoBehaviour
    {
        public void LaunchWithLayout(string layoutPath, bool createTempCopy = true)
        {


            // create temp folder for layout (parhaps with 'safe name' with all chars removed?)

            // did we need to do the thing of copying all of Mfme stuff to this too?)

            // launch the Mfme process
            // do we need to use the dll (annoying as trips up virus checkers with false positives?)
            // so we don't affect the Mfme registry?

            // Another option could be to backup a copy of the existing Mfme windows registry, or rename it
            // then restore it afterwards?

            // Get the window handle

            // test by printing window title, does this work?

        }
    }
}
