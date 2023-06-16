namespace Oasis
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Oasis.UI;
    using UnityWinForms.Examples;


    public class UIController : MonoBehaviour
    {
        private void Start()
        {
            var form = new UI.UI();

            //var form = new FormExamples();

            //Controls.add

            form.Show();
        }
    }
}

