using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace CyberScreenSaver
{
    class Program
    {
        // Win32
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern int ShowCursor(bool show);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int idx, uint val);
        [DllImport("user32.dll")] static extern uint GetWindowLong(IntPtr hwnd, int idx);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

        const int GWL_STYLE = -16;
        const int GWL_EXSTYLE = -20;
        const uint WS_POPUP = 0x80000000;
        const uint WS_VISIBLE = 0x10000000;
        const uint WS_EX_TOPMOST = 0x00000008;
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        const uint SWP_FRAMECHANGED = 0x0020;
        const uint SWP_SHOWWINDOW = 0x0040;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        static string EdgePath = "";
        static string InstallDir = "";
        static string Mode = "combo";
        static volatile bool Running = true;

        // Network scanner state
        static List<Dictionary<string, object>> LiveDevices = new List<Dictionary<string, object>>();
        static readonly object DeviceLock = new object();
        static string LocalIP = "";
        static string Subnet = "";
        static TcpListener ApiListener;

        [STAThread]
        static void Main(string[] args)
        {
            string exeName = Path.GetFileNameWithoutExtension(
                Assembly.GetExecutingAssembly().Location).ToLower();

            if (exeName.Contains("oscilloscope")) Mode = "oscilloscope";
            else if (exeName.Contains("clock")) Mode = "clock";
            else if (exeName.Contains("livenetwork")) Mode = "livenetwork";
            else if (exeName.Contains("network") && !exeName.Contains("live")) Mode = "network";
            else Mode = "combo";

            EdgePath = FindEdge();
            if (string.IsNullOrEmpty(EdgePath))
            {
                MessageBox(IntPtr.Zero, "Microsoft Edge not found.", "CyberScreenSaver", 0x10);
                return;
            }

            InstallDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CyberScreenSaver");
            Directory.CreateDirectory(InstallDir);
            ExtractHtmlFiles();

            string mode = "run";
            foreach (string arg in args)
            {
                string a = arg.TrimStart('/', '-').ToLower();
                if (a == "s") mode = "run";
                else if (a == "c") mode = "config";
                else if (a == "p") mode = "preview";
            }

            if (mode == "config")
            {
                string name = Mode == "oscilloscope" ? "CyberOscilloscope" :
                              Mode == "clock" ? "CyberClock" :
                              Mode == "network" ? "CyberNetwork" :
                              Mode == "livenetwork" ? "CyberLiveNetwork" : "CyberScreenSaver";
                string desc = Mode == "livenetwork"
                    ? "Live network topology screensaver.\nScans your local network and displays real devices."
                    : "Cyberpunk terminal screensaver.\nShows on all monitors.";
                MessageBox(IntPtr.Zero, name + "\n\n" + desc + "\n\nNo configuration needed.", name, 0x40);
            }
            else if (mode == "run")
            {
                RunScreensaver();
            }
        }

        static string FindEdge()
        {
            string[] paths = new[] {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            };
            foreach (string p in paths) if (File.Exists(p)) return p;
            return "";
        }

        static void ExtractHtmlFiles()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string resName in asm.GetManifestResourceNames())
            {
                if (!resName.EndsWith(".html")) continue;
                // Resource name is the full path used in /resource: flag
                // Extract just the filename part
                string fname = Path.GetFileName(resName);
                string dst = Path.Combine(InstallDir, fname);
                using (Stream src = asm.GetManifestResourceStream(resName))
                using (FileStream fs = File.Create(dst))
                {
                    src.CopyTo(fs);
                }
            }
        }

        struct MonitorInfo { public int X, Y, W, H; }

        static List<MonitorInfo> GetMonitors()
        {
            var monitors = new List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data)
            {
                monitors.Add(new MonitorInfo {
                    X = rect.Left, Y = rect.Top,
                    W = rect.Right - rect.Left, H = rect.Bottom - rect.Top
                });
                return true;
            }, IntPtr.Zero);
            monitors.Sort((a, b) => a.X.CompareTo(b.X));
            return monitors;
        }

        static void MakeFullscreen(IntPtr hwnd, int x, int y, int w, int h)
        {
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            uint ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, w, h, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        static List<IntPtr> FindScreensaverWindows(int expected, int maxWaitMs)
        {
            string[] keywords = { "intrusion", "signal analysis", "oscilloscope",
                "network topology", "cyber-core", "datarain", "live network" };
            var found = new Dictionary<IntPtr, string>();
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < maxWaitMs && found.Count < expected)
            {
                EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    int len = GetWindowTextLength(hwnd);
                    if (len <= 0) return true;
                    StringBuilder sb = new StringBuilder(len + 1);
                    GetWindowText(hwnd, sb, sb.Capacity);
                    string title = sb.ToString().ToLower();
                    foreach (string kw in keywords)
                    {
                        if (title.Contains(kw) && !found.ContainsKey(hwnd))
                        {
                            found[hwnd] = title;
                            break;
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                if (found.Count < expected) Thread.Sleep(500);
            }
            return new List<IntPtr>(found.Keys);
        }

        static void CreateEdgeProfile(string path)
        {
            string defaultDir = Path.Combine(path, "Default");
            Directory.CreateDirectory(defaultDir);
            File.WriteAllText(Path.Combine(defaultDir, "Preferences"),
                "{\"browser\":{\"has_seen_welcome_page\":true,\"should_reset_check_default_browser\":false},\"sync\":{\"has_setup_completed\":true,\"requested\":false},\"signin\":{\"allowed\":false,\"allowed_on_next_startup\":false},\"profile\":{\"default_content_setting_values\":{\"notifications\":2}},\"distribution\":{\"import_bookmarks\":false,\"import_history\":false,\"import_search_engine\":false,\"suppress_first_run_bubble\":true,\"suppress_first_run_default_browser_prompt\":true,\"skip_first_run_ui\":true,\"make_chrome_default\":false,\"make_chrome_default_for_user\":false},\"first_run_tabs\":{\"has_been_shown\":true}}");
            File.WriteAllText(Path.Combine(path, "First Run"), "");
            File.WriteAllText(Path.Combine(path, "Local State"),
                "{\"browser\":{\"enabled_labs_experiments\":[]},\"user_experience_metrics\":{\"reporting_enabled\":false}}");
        }

        static string[] GetHtmlFiles()
        {
            if (Mode == "oscilloscope") return new[] { "screensaver_oscilloscope.html" };
            if (Mode == "clock") return new[] { "screensaver_datarain.html" };
            if (Mode == "network") return new[] { "screensaver_network.html" };
            if (Mode == "livenetwork") return new[] { "screensaver_livenetwork.html" };
            return new[] { "screensaver_oscilloscope.html", "screensaver_datarain.html", "screensaver_network.html" };
        }

        static void RunScreensaver()
        {
            var monitors = GetMonitors();
            if (monitors.Count == 0) return;

            if (Mode == "livenetwork")
            {
                StartNetworkScanner();
                StartApiServer();
                Thread.Sleep(2000);
            }

            string[] htmlFiles = GetHtmlFiles();
            var processes = new List<Process>();
            string tempBase = Path.Combine(Path.GetTempPath(), "cyberscr_native");

            for (int i = 0; i < monitors.Count; i++)
            {
                string d = tempBase + "_" + i;
                try { Directory.Delete(d, true); } catch { }
                CreateEdgeProfile(d);
            }

            string edgeArgs = "--no-first-run --no-default-browser-check --disable-sync " +
                "--disable-extensions --disable-infobars --disable-notifications " +
                "--disable-popup-blocking --disable-session-crashed-bubble " +
                "--disable-features=TranslateUI,msEdgeSidebarV2,msEdgeJSONViewer,msEdgeMoveToDesktopButton " +
                "--disable-component-update --disable-background-networking " +
                "--disable-domain-reliability --autoplay-policy=no-user-gesture-required " +
                "--suppress-message-center-popups --ash-no-nudges --noerrdialogs " +
                "--kiosk --force-app-mode";

            for (int i = 0; i < monitors.Count; i++)
            {
                MonitorInfo mon = monitors[i];
                string html = htmlFiles[i % htmlFiles.Length];
                string path = Path.Combine(InstallDir, html);
                if (!File.Exists(path)) continue;

                string url = "file:///" + path.Replace('\\', '/');
                string args = string.Format("--app={0} --start-fullscreen --window-position={1},{2} --window-size={3},{4} --user-data-dir={5}_{6} {7}",
                    url, mon.X, mon.Y, mon.W, mon.H, tempBase, i, edgeArgs);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = EdgePath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try { processes.Add(Process.Start(psi)); } catch { }
            }

            if (processes.Count == 0) return;

            Thread.Sleep(3000);

            List<IntPtr> windows = FindScreensaverWindows(monitors.Count, 10000);
            for (int i = 0; i < Math.Min(windows.Count, monitors.Count); i++)
            {
                MonitorInfo mon = monitors[i];
                MakeFullscreen(windows[i], mon.X, mon.Y, mon.W, mon.H);
                Thread.Sleep(100);
                MakeFullscreen(windows[i], mon.X, mon.Y, mon.W, mon.H);
            }

            ShowCursor(false);
            Thread.Sleep(3000);

            POINT startPos;
            GetCursorPos(out startPos);
            int threshold = 15;

            try
            {
                while (true)
                {
                    Thread.Sleep(100);

                    POINT pos;
                    GetCursorPos(out pos);
                    if (Math.Abs(pos.X - startPos.X) > threshold || Math.Abs(pos.Y - startPos.Y) > threshold)
                        break;

                    bool keyPressed = false;
                    for (int vk = 8; vk < 256; vk++)
                    {
                        if ((GetAsyncKeyState(vk) & 0x8000) != 0) { keyPressed = true; break; }
                    }
                    if (keyPressed) break;
                }
            }
            finally
            {
                Running = false;
                ShowCursor(true);
                if (ApiListener != null) try { ApiListener.Stop(); } catch { }

                // Close screensaver Edge windows by sending WM_CLOSE, then kill if needed
                foreach (IntPtr wnd in FindScreensaverWindows(monitors.Count, 1000))
                {
                    try { SendMessage(wnd, 0x0010, IntPtr.Zero, IntPtr.Zero); } catch { } // WM_CLOSE
                }
                Thread.Sleep(1000);
                foreach (Process p in processes)
                {
                    try { if (!p.HasExited) p.Kill(); } catch { }
                }

                Thread.Sleep(500);
                for (int i = 0; i < monitors.Count; i++)
                {
                    try { Directory.Delete(tempBase + "_" + i, true); } catch { }
                }

                Environment.Exit(0);
            }
        }

        // ── Network Scanner ──

        static void StartNetworkScanner()
        {
            LocalIP = GetLocalIP();
            string[] parts = LocalIP.Split('.');
            Subnet = parts[0] + "." + parts[1] + "." + parts[2];

            Thread thread = new Thread(() =>
            {
                ScanNetwork(true);
                while (Running)
                {
                    Thread.Sleep(30000);
                    try { ScanNetwork(false); } catch { }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        static string GetLocalIP()
        {
            try
            {
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("8.8.8.8", 80);
                    return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
                }
            }
            catch { return "192.168.1.1"; }
        }

        static void ScanNetwork(bool quick)
        {
            Dictionary<string, string> arp = GetArpTable();
            HashSet<string> ips = new HashSet<string>(arp.Keys);

            if (!quick)
            {
                foreach (int i in new[] { 1, 2, 100, 110, 150, 200, 247, 248, 250, 251, 252, 253, 254 })
                    ips.Add(Subnet + "." + i);
                for (int i = 1; i <= 30; i++)
                    ips.Add(Subnet + "." + i);
            }

            var results = new Dictionary<string, Dictionary<string, object>>();
            var threads = new List<Thread>();

            foreach (string ip in ips)
            {
                string capturedIp = ip;
                Thread t = new Thread(() =>
                {
                    bool online = arp.ContainsKey(capturedIp) || PingHost(capturedIp);
                    if (!online) return;

                    string hostname = ResolveHostname(capturedIp);
                    if (hostname == null) hostname = "DEVICE-" + capturedIp.Split('.').Last();
                    string mac = arp.ContainsKey(capturedIp) ? arp[capturedIp] : "UNKNOWN";
                    bool isSelf = capturedIp == LocalIP;

                    string type = GuessType(hostname, capturedIp, isSelf);

                    var dev = new Dictionary<string, object> {
                        {"ip", capturedIp}, {"mac", mac}, {"hostname", hostname},
                        {"type", type}, {"status", "online"}, {"is_self", isSelf}
                    };

                    lock (results) results[capturedIp] = dev;
                });
                t.IsBackground = true;
                threads.Add(t);
                t.Start();
            }

            foreach (Thread t in threads) t.Join(5000);

            var sorted = results.OrderBy(kv =>
            {
                string[] p = kv.Key.Split('.');
                return int.Parse(p[0]) * 16777216 + int.Parse(p[1]) * 65536 + int.Parse(p[2]) * 256 + int.Parse(p[3]);
            }).Select(kv => kv.Value).ToList();

            lock (DeviceLock) { LiveDevices = sorted; }
        }

        static Dictionary<string, string> GetArpTable()
        {
            var devices = new Dictionary<string, string>();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("arp", "-a")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                Process p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);

                foreach (string line in output.Split('\n'))
                {
                    string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && parts[0].Count(c => c == '.') == 3)
                    {
                        string ip = parts[0];
                        string mac = parts[1].Replace('-', ':').ToUpper();
                        if (mac != "FF:FF:FF:FF:FF:FF" && !ip.EndsWith(".255"))
                            devices[ip] = mac;
                    }
                }
            }
            catch { }
            return devices;
        }

        static bool PingHost(string ip)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(ip, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch { return false; }
        }

        static string ResolveHostname(string ip)
        {
            // Try DNS first
            try
            {
                string name = Dns.GetHostEntry(ip).HostName;
                if (name.Contains('.')) name = name.Split('.')[0];
                // If DNS just returns the IP back, it didn't resolve
                if (name != ip && !name.StartsWith("192") && !name.StartsWith("10.") && !name.StartsWith("172."))
                    return name.ToUpper();
            }
            catch { }

            // Try NetBIOS / nbtstat
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("nbtstat", "-A " + ip)
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                Process p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                foreach (string line in output.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Contains("<00>") && trimmed.Contains("UNIQUE"))
                    {
                        string name = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                        if (name.Length > 0 && name != "IS~" && !name.StartsWith("__"))
                            return name.ToUpper();
                    }
                }
            }
            catch { }

            return null;
        }

        static string GuessType(string hostname, string ip, bool isSelf)
        {
            string name = (hostname ?? "").ToLower();
            int last = int.Parse(ip.Split('.').Last());

            if (isSelf) return "workstation";
            if (last == 1 || name.Contains("pfsense") || name.Contains("opnsense") || name.Contains("gateway") || name.Contains("router")) return "router";
            if (name.Contains("switch")) return "switch";
            if (name.Contains("ap") || name.Contains("ubiquiti") || name.Contains("unifi")) return "access_point";
            if (name.Contains("nas") || name.Contains("synology") || name.Contains("qnap")) return "storage";
            if (name.Contains("cam") || name.Contains("reolink") || name.Contains("axis") || name.Contains("ipc")) return "camera";
            if (name.Contains("proxmox") || name.Contains("pve") || name.Contains("esxi")) return "hypervisor";
            if (name.Contains("docker") || name.Contains("container")) return "container";
            if (name.Contains("printer") || name.Contains("hp") || name.Contains("epson")) return "printer";
            if (name.Contains("pi") || name.Contains("raspberry") || name.Contains("esp")) return "iot";
            if (name.Contains("tv") || name.Contains("roku") || name.Contains("fire")) return "media";
            if (name.Contains("phone") || name.Contains("iphone") || name.Contains("android") || name.Contains("galaxy")) return "mobile";
            return "server";
        }

        static void StartApiServer()
        {
            Thread thread = new Thread(() =>
            {
                try
                {
                    ApiListener = new TcpListener(IPAddress.Loopback, 5050);
                    ApiListener.Start();

                    while (Running)
                    {
                        if (!ApiListener.Pending()) { Thread.Sleep(50); continue; }
                        try
                        {
                            using (TcpClient client = ApiListener.AcceptTcpClient())
                            using (NetworkStream stream = client.GetStream())
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                string requestLine = reader.ReadLine();
                                string line;
                                while ((line = reader.ReadLine()) != null && line.Length > 0) { }

                                string json = "{}";
                                if (requestLine != null && requestLine.Contains("/api/network"))
                                {
                                    List<Dictionary<string, object>> devices;
                                    lock (DeviceLock) { devices = new List<Dictionary<string, object>>(LiveDevices); }

                                    var data = new Dictionary<string, object> {
                                        {"devices", devices},
                                        {"subnet", Subnet + ".0/24"},
                                        {"scan_time", (long)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds},
                                        {"local_ip", LocalIP}
                                    };
                                    json = new JavaScriptSerializer().Serialize(data);
                                }

                                string response = "HTTP/1.1 200 OK\r\n" +
                                    "Content-Type: application/json\r\n" +
                                    "Access-Control-Allow-Origin: *\r\n" +
                                    "Connection: close\r\n" +
                                    "Content-Length: " + Encoding.UTF8.GetByteCount(json) + "\r\n" +
                                    "\r\n" + json;
                                byte[] buf = Encoding.UTF8.GetBytes(response);
                                stream.Write(buf, 0, buf.Length);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
