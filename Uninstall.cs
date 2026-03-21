using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CyberScreenSaverUninstall
{
    class Program
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int ShellExecute(IntPtr hwnd, string op, string file, string args, string dir, int show);

        static void Main(string[] args)
        {
            // Check admin by trying to write to System32
            try
            {
                string test = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cyberscr_test.tmp");
                File.WriteAllText(test, "");
                File.Delete(test);
            }
            catch
            {
                // Re-launch as admin
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                ShellExecute(IntPtr.Zero, "runas", exe, "", "", 1);
                return;
            }

            Console.WriteLine("==================================================");
            Console.WriteLine("  CyberScreenSaver Uninstaller");
            Console.WriteLine("==================================================");
            Console.WriteLine();

            int removed = 0;
            int failed = 0;

            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string[] scrFiles = new[] {
                "CyberScreenSaver.scr", "CyberOscilloscope.scr",
                "CyberClock.scr", "CyberNetwork.scr", "CyberLiveNetwork.scr"
            };

            Console.WriteLine("[1/2] Removing screensavers from System32...");
            foreach (string scr in scrFiles)
            {
                string path = Path.Combine(sys32, scr);
                if (File.Exists(path))
                {
                    try { File.Delete(path); removed++; Console.WriteLine("  REMOVED: " + scr); }
                    catch (Exception e) { failed++; Console.WriteLine("  FAILED:  " + scr + " (" + e.Message + ")"); }
                }
                else Console.WriteLine("  SKIP:    " + scr + " (not found)");
            }

            string cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CyberScreenSaver");

            Console.WriteLine();
            Console.WriteLine("[2/2] Removing cached data...");
            if (Directory.Exists(cache))
            {
                try { Directory.Delete(cache, true); removed++; Console.WriteLine("  REMOVED: " + cache); }
                catch (Exception e) { failed++; Console.WriteLine("  FAILED:  " + cache + " (" + e.Message + ")"); }
            }
            else Console.WriteLine("  SKIP:    Cache directory not found");

            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------");
            if (failed > 0)
                Console.WriteLine("Done. Removed " + removed + " items, " + failed + " failed.");
            else
                Console.WriteLine("Done. Removed " + removed + " items. All clean.");
            Console.WriteLine();
            Console.Write("Press Enter to close...");
            Console.ReadLine();
        }
    }
}
