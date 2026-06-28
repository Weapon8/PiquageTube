using System;
using System.Windows.Forms;

namespace PiquageTube
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.Run(new MainForm());
        }
    }
}
