using System;
using System.Threading;
using System.Windows.Forms;
using AutoDaily.UI.Forms;

namespace AutoDaily
{
    static class Program
    {
        private static Mutex mutex = null;
        private const string AppName = "AutoDaily_SingleInstance";

        [STAThread]
        static void Main()
        {
            // 单实例检查
            bool createdNew;
            mutex = new Mutex(true, AppName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("AutoDaily 已经在运行中。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
        }
    }
}

