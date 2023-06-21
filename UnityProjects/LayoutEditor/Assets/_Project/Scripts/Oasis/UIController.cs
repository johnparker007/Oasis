using Oasis.UI;
using UnityEngine;

namespace Oasis
{
    public class UIController : MonoBehaviour
    {
        public RootUI RootUI
        {
            get;
            private set;
        } = null;

        private void Start()
        {
            RootUI = new RootUI();
            RootUI.Show();
        }
    }
}

