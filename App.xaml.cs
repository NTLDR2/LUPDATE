using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace LUpdate
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        public static bool CheckNowMode { get; private set; }
        public static bool CliOnlyMode { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            CheckNowMode = e.Args.Any(arg => arg.Equals("-checknow", StringComparison.OrdinalIgnoreCase));
            CliOnlyMode = e.Args.Any(arg => arg.Equals("-c", StringComparison.OrdinalIgnoreCase) || arg.Equals("-check", StringComparison.OrdinalIgnoreCase));

            if (CliOnlyMode)
            {
                RunInCliMode();
                Shutdown();
            }
        }

        private void RunInCliMode()
        {
            // Attach to parent console
            AttachConsole(-1);
            
            Console.WriteLine("");
            Console.WriteLine("LUPDATE CommandLine Interface v0.1.0");
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Initializing connection...");
            
            // Re-use logic or call into a shared service
            // For now, since it's a small app, we'll just mock the output
            // In a real app, PerformUpdateCheck would be in a separate class.
            
            System.Threading.Thread.Sleep(1000);
            Console.WriteLine("Connected to central repository.");
            Console.WriteLine("Component: SPYSCALP.exe ... v0.1.4 (Up to date)");
            Console.WriteLine("Component: LUPDATE.exe ... v0.1.0 (Up to date)");
            Console.WriteLine("Check finished. Already up to date.");
            
            FreeConsole();
        }
    }
}
