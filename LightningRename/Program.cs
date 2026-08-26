using System;
using System.Windows.Forms;

namespace LightningRename
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            if (args.Length > 0)
                form.AddInitialPaths(args);
            Application.Run(form);
            return 0;
        }
    }
}
