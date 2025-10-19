// Encoding: UTF-8
using System;
using System.Windows.Forms;

namespace ShapefileViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var mainForm = new MainForm())
            {
                Application.Run(mainForm);
            }
        }
    }
}