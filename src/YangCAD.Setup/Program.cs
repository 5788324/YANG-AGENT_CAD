using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YangCAD.Setup
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                AttachConsole(-1);
                var actionText = args[0].TrimStart('/', '-').ToLowerInvariant();
                var purge = args.Any(a => a.IndexOf("purge", StringComparison.OrdinalIgnoreCase) >= 0);
                var service = new InstallerService(AppDomain.CurrentDomain.BaseDirectory);
                SetupAction action;
                switch (actionText)
                {
                    case "install": action = SetupAction.Install; break;
                    case "update": action = SetupAction.Update; break;
                    case "repair": action = SetupAction.Repair; break;
                    case "uninstall": action = SetupAction.Uninstall; break;
                    default:
                        Console.WriteLine("未知操作：" + actionText);
                        return 1;
                }
                try
                {
                    var msg = service.Execute(action, purge, Console.WriteLine);
                    Console.WriteLine(msg);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("操作失败：" + ex.Message);
                    return 2;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}

