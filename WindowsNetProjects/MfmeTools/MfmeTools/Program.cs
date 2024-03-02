using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MfmeTools.Mfme;

namespace MfmeTools
{
    static class Program
    {
        public static MainForm MainForm;
        public static Configuration Configuration = new Configuration();
        public static Extractor Extractor = new Extractor();
        public static ExeCopier ExeCopier = new ExeCopier();
        public static LayoutCopier LayoutCopier = new LayoutCopier();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Initialise();

            Application.Run(MainForm);
        }

        private static void Initialise()
        {
            MainForm = new MainForm();

            ExeCopier.Initialise();
        }
    }
}
