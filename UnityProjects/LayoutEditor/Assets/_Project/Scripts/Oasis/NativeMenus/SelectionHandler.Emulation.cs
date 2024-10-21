using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnEmulationStart()
        {
            Editor.Instance.MameController.StartMame(false);
        }

        public void OnEmulationExit()
        {
            Editor.Instance.MameController.ExitMame();
        }

        public void OnEmulationPause()
        {
            Editor.Instance.MameController.Pause();
        }

        public void OnEmulationResume()
        {
            Editor.Instance.MameController.Resume();
        }

        public void OnEmulationSoftReset()
        {
            Editor.Instance.MameController.SoftReset();
        }

        public void OnEmulationHardReset()
        {
            Editor.Instance.MameController.HardReset();
        }

        public void OnEmulationThrottled()
        {
            Editor.Instance.MameController.SetThrottled(true);
        }

        public void OnEmulationUnthrottled()
        {
            Editor.Instance.MameController.SetThrottled(false);
        }

        public void OnEmulationStateLoad()
        {
            Editor.Instance.MameController.StateLoad();
        }

        public void OnEmulationStateSave()
        {
            Editor.Instance.MameController.StateSave();
        }

        public void OnEmulationStateSaveAndExit()
        {
            Editor.Instance.MameController.StateSaveAndExit();
        }

        public void OnEmulationStartAndStateLoad()
        {
            Editor.Instance.MameController.StartMame(true);
        }
    }
}
