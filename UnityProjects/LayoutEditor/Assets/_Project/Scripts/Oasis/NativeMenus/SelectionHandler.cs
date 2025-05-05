using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        private const bool kDebugForceMfmeImportOnStartup = false;
        private const bool kDebugForceProjectLoadOnStartup = false;

        private void Start()
        {
            if(kDebugForceMfmeImportOnStartup)
            {
                OnFileImportMfme();
            }

            if(kDebugForceProjectLoadOnStartup)
            {
                // TODO: add this call once Paul's changes including Oasis Project loading in mainline
            }
        }
    }
}
