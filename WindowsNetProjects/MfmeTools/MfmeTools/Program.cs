using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MfmeTools.MFME;

namespace MfmeTools
{
    static class Program
    {
        public static Extractor Extractor = new Extractor();
        public static ExeCopier ExeCopier = new ExeCopier();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Initialise();

            Application.Run(new MainForm());
        }

        private static void Initialise()
        {
            ExeCopier.Initialise();
        }
    }
}
