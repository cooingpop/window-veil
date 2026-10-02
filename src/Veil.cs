using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 창 가림.
// 트레이에 상주하며, 고른 프로그램의 창을 따라다니다가 그 창을 쓰지 않을 때만 흐린 판으로 덮는다.
// 덮인 창 위에 마우스를 올리고 Ctrl 을 누르고 있으면 잠깐 보인다.
static class Win {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr h, ref CompAttr d);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct AccentPolicy { public int State; public int Flags; public uint Color; public int Anim; }
    [StructLayout(LayoutKind.Sequential)] public struct CompAttr { public int Attr; public IntPtr Data; public int Size; }

    public const uint GW_HWNDPREV = 3;
    public const uint GW_OWNER = 4;
    public const uint GA_ROOTOWNER = 3;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const int VK_CONTROL = 0x11;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
}

class Veil : Form {
    public readonly IntPtr Target;
    public readonly bool Owned;
    public Rectangle Last = Rectangle.Empty;
    public string State = "";

    public Veil(IntPtr target) {
        Target = target;
        // 딸린 창(카카오톡 하단 광고 띠 등)인지. 기록에만 쓴다.
        Owned = Win.GetWindow(target, Win.GW_OWNER) != IntPtr.Zero;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Bounds = new Rectangle(-32000, -32000, 10, 10);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    // 포커스를 뺏지 않고(NOACTIVATE), 작업 표시줄과 Alt+Tab 에 안 뜨게(TOOLWINDOW) 한다.
    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; }
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        // 4 = 아크릴 흐림. 색은 AABBGGRR · 진한 회색을 넉넉히 섞어 밝은 화면 위에서도 글씨가 안 읽히게 한다.
        var ap = new Win.AccentPolicy { State = 4, Flags = 2, Color = 0xD0242424 };
        int size = Marshal.SizeOf(ap);
        IntPtr p = Marshal.AllocHGlobal(size);
        try {
            Marshal.StructureToPtr(ap, p, false);
            var d = new Win.CompAttr { Attr = 19, Data = p, Size = size };
            Win.SetWindowCompositionAttribute(Handle, ref d);
        } finally { Marshal.FreeHGlobal(p); }
    }

    protected override void WndProc(ref Message m) {
        const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
        base.WndProc(ref m);
    }

    // 가림을 클릭하면 덮여 있던 창으로 넘어간다. 다음 점검 때 그 창이 맨 앞이므로 가림이 걷힌다.
    protected override void OnMouseDown(MouseEventArgs e) { Win.SetForegroundWindow(Target); }

    // 가림 위에는 아무것도 그리지 않는다. 글씨가 있으면 가려져 있다는 사실 자체가 드러난다.
}

// 방패 아이콘 메뉴의 「사용법 보기」. 할 일별로 묶어 보여준다.
class HelpForm : Form {
    static readonly string[][] Sections = {
        new[] { "프로그램 가리기", "작업 표시줄의 방패 아이콘을 누르고, 목록에서 가릴 프로그램을 체크합니다. 체크한 프로그램은 다른 창을 쓰는 동안 흐리게 덮입니다. 메뉴가 닫히지 않으니 여러 개를 이어서 체크할 수 있습니다." },
        new[] { "잠깐 보기", "덮인 창 위에 마우스를 올리고 Ctrl 을 누르고 있으면 보입니다. 손을 떼면 다시 덮입니다." },
        new[] { "그 창으로 돌아가기", "덮인 창을 클릭하면 그 창으로 넘어가고 가림이 걷힙니다. 작업 표시줄이나 Alt+Tab 으로 넘어가도 같습니다." },
        new[] { "가림 그만두기", "목록에서 체크를 풀면 그 프로그램만 그만 가립니다. 전부 잠깐 멈추려면 「모든 가림 잠시 끄기」, 프로그램을 닫으려면 「창 가림 끝내기」 를 누릅니다." },
        new[] { "저장하는 것", "고른 프로그램의 이름만 %APPDATA%\\WindowVeil\\targets.txt 에 저장합니다. 창 제목이나 화면 내용은 저장하지 않습니다." },
    };

    public HelpForm() {
        Text = "창 가림 사용법";
        Icon = SystemIcons.Shield;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Malgun Gothic", 10);

        int width;
        using (var g = CreateGraphics()) width = (int)(440 * g.DpiX / 96f);
        var bold = new Font(Font, FontStyle.Bold);
        var flow = new FlowLayoutPanel {
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18, 8, 18, 16)
        };
        foreach (var s in Sections) {
            flow.Controls.Add(new Label { Text = s[0], Font = bold, AutoSize = true, Margin = new Padding(0, 12, 0, 3) });
            flow.Controls.Add(new Label { Text = s[1], AutoSize = true, MaximumSize = new Size(width, 0), Margin = new Padding(0) });
        }
        var close = new Button { Text = "닫기", AutoSize = true, Margin = new Padding(0, 18, 0, 0) };
        close.Click += (s, e) => Close();
        flow.Controls.Add(close);
        AcceptButton = close; CancelButton = close;
        Controls.Add(flow);
    }
}

public static class VeilApp {
    struct WinInfo { public IntPtr H; public uint Pid; public bool Minimized; }

    // 바탕화면과 작업 표시줄. 이 창들을 덮으면 화면 전체가 막힌다.
    static readonly HashSet<string> ShellClasses = new HashSet<string> { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    static string logPath;
    static string settingsPath;
    static uint selfPid;
    static DateTime endAt = DateTime.MaxValue;
    static bool enabled = true;
    static int tick;
    static readonly HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<uint, string> pidNames = new Dictionary<uint, string>();
    static readonly Dictionary<IntPtr, Veil> veils = new Dictionary<IntPtr, Veil>();
    static NotifyIcon tray;
    static ContextMenuStrip menu;
    static ToolStripMenuItem statusItem;
    static HelpForm helpForm;
    static System.Windows.Forms.Timer timer;
    static bool keepMenuOpen;

    public static void Run(int minutes, string log) {
        bool first;
        using (var single = new Mutex(true, @"Local\WindowVeil", out first)) {
            if (!first) return; // 이미 떠 있으면 하나만 둔다.
            Start(minutes, log);
            GC.KeepAlive(single);
        }
    }

    static void Start(int minutes, string log) {
        Win.SetProcessDPIAware();
        logPath = log;
        selfPid = (uint)Process.GetCurrentProcess().Id;
        if (minutes > 0) endAt = DateTime.Now.AddMinutes(minutes);
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowVeil");
        Directory.CreateDirectory(dir);
        settingsPath = Path.Combine(dir, "targets.txt");
        bool firstRun = !File.Exists(settingsPath);
        LoadSelection();

        Application.EnableVisualStyles();
        menu = new ContextMenuStrip();
        menu.Opening += (s, e) => { BuildMenu(); e.Cancel = false; };
        menu.ItemClicked += (s, e) => { keepMenuOpen = e.ClickedItem.Tag is string; };
        // 프로그램을 체크할 때는 메뉴를 닫지 않는다. 여러 개를 이어서 고를 수 있게.
        menu.Closing += (s, e) => {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && keepMenuOpen) e.Cancel = true;
            keepMenuOpen = false;
        };

        // 마우스를 올렸을 때 뜨는 글씨는 이름만 둔다. 상태와 사용법은 메뉴 안에서 보여준다.
        tray = new NotifyIcon { Icon = SystemIcons.Shield, Visible = true, ContextMenuStrip = menu, Text = "창 가림" };
        tray.MouseUp += (s, e) => {
            if (e.Button != MouseButtons.Left) return;
            var show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            if (show != null) show.Invoke(tray, null);
        };
        if (firstRun || selected.Count == 0)
            tray.ShowBalloonTip(8000, "창 가림이 켜졌습니다",
                "작업 표시줄의 방패 아이콘을 눌러 가릴 프로그램을 체크하세요. 사용법도 그 메뉴에 있습니다.", ToolTipIcon.Info);
        else
            tray.ShowBalloonTip(5000, "창 가림이 켜졌습니다",
                selected.Count + "개 프로그램을 가립니다. 바꾸려면 방패 아이콘을 누르세요.", ToolTipIcon.Info);

        timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (s, e) => Tick();
        timer.Start();
        Log("시작 · 고른 프로그램 " + selected.Count + "개" + (minutes > 0 ? " · " + minutes + "분 뒤 종료" : ""));
        Application.Run();
    }

    // ── 트레이 메뉴 ──────────────────────────────────────────

    static void BuildMenu() {
        foreach (ToolStripItem old in menu.Items) if (old.Image != null) old.Image.Dispose();
        menu.Items.Clear();
        statusItem = new ToolStripMenuItem(StatusText()) { Enabled = false };
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("가릴 프로그램 고르기 · 체크하면 쓰지 않을 때 가려집니다") { Enabled = false });

        var running = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in EnumAppWindows()) {
            string n = NameOf(w.Pid);
            if (n != null && !running.ContainsKey(n)) running[n] = w.Pid;
        }

        var rows = new List<KeyValuePair<string, ToolStripMenuItem>>();
        foreach (var name in running.Keys.Union(selected, StringComparer.OrdinalIgnoreCase)) {
            uint pid;
            string path = running.TryGetValue(name, out pid) ? PathOf(pid) : null;
            string label = DisplayName(name, path) + (running.ContainsKey(name) ? "" : " · 지금 꺼져 있음");
            var item = new ToolStripMenuItem(label) { Tag = name, Checked = selected.Contains(name), Image = IconOf(path) };
            item.Click += (s, e) => Toggle((ToolStripMenuItem)s);
            rows.Add(new KeyValuePair<string, ToolStripMenuItem>(label, item));
        }
        if (rows.Count == 0)
            menu.Items.Add(new ToolStripMenuItem("지금 창이 떠 있는 프로그램이 없습니다") { Enabled = false });
        foreach (var row in rows.OrderBy(r => r.Key, StringComparer.CurrentCultureIgnoreCase))
            menu.Items.Add(row.Value);

        menu.Items.Add(new ToolStripSeparator());
        var help = new ToolStripMenuItem("사용법 보기");
        help.Click += (s, e) => ShowHelp();
        menu.Items.Add(help);
        var pause = new ToolStripMenuItem(enabled ? "모든 가림 잠시 끄기" : "모든 가림 다시 켜기");
        pause.Click += (s, e) => {
            enabled = !enabled;
            Log(enabled ? "가림 다시 켬" : "가림 잠시 끔");
        };
        menu.Items.Add(pause);
        var quit = new ToolStripMenuItem("창 가림 끝내기");
        quit.Click += (s, e) => Quit("메뉴에서 끝냄");
        menu.Items.Add(quit);
    }

    static void Toggle(ToolStripMenuItem item) {
        string name = (string)item.Tag;
        if (selected.Contains(name)) selected.Remove(name); else selected.Add(name);
        item.Checked = selected.Contains(name);
        SaveSelection();
        if (statusItem != null) statusItem.Text = StatusText();
        Log((item.Checked ? "가림 대상 추가 · " : "가림 대상 해제 · ") + name);
    }

    static string StatusText() {
        return !enabled ? "모든 가림이 잠시 꺼져 있습니다"
            : selected.Count == 0 ? "가릴 프로그램을 아직 고르지 않았습니다"
            : "지금 " + selected.Count + "개 프로그램을 가리고 있습니다";
    }

    static void ShowHelp() {
        if (helpForm != null && !helpForm.IsDisposed) { helpForm.Activate(); return; }
        helpForm = new HelpForm();
        helpForm.Show();
    }

    static string DisplayName(string name, string path) {
        if (path != null) {
            try {
                var v = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(v.FileDescription)) return v.FileDescription.Trim();
                if (!string.IsNullOrWhiteSpace(v.ProductName)) return v.ProductName.Trim();
            } catch (FileNotFoundException) { } catch (UnauthorizedAccessException) { }
        }
        return name;
    }

    static Image IconOf(string path) {
        if (path == null) return null;
        try { using (var ico = Icon.ExtractAssociatedIcon(path)) return ico == null ? null : ico.ToBitmap(); }
        catch (ArgumentException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
    }

    // ── 고른 목록 저장 · 프로그램 이름만 저장한다. 창 제목은 남기지 않는다 ──

    static void LoadSelection() {
        selected.Clear();
        if (!File.Exists(settingsPath)) return;
        foreach (var line in File.ReadAllLines(settingsPath, Encoding.UTF8)) {
            string n = line.Trim();
            if (IsValidName(n)) selected.Add(n);
        }
    }

    static void SaveSelection() {
        try { File.WriteAllLines(settingsPath, selected.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(), Encoding.UTF8); }
        catch (IOException ex) { Log("고른 목록 저장 실패 · " + ex.Message); }
    }

    static bool IsValidName(string n) {
        return n.Length > 0 && n.Length <= 100 && n.All(c => !char.IsControl(c) && c != '\\' && c != '/');
    }

    // ── 창 따라가기 ──────────────────────────────────────────

    static void Tick() {
        if (DateTime.Now > endAt) { Quit("시간이 다 되어 끝냄"); return; }
        if (tick++ % 40 == 0) pidNames.Clear(); // 끝난 프로세스 번호가 재사용될 수 있어 2초마다 비운다.

        var wins = new List<IntPtr>();
        if (enabled && selected.Count > 0)
            foreach (var w in EnumAppWindows())
                if (!w.Minimized && selected.Contains(NameOf(w.Pid) ?? "")) wins.Add(w.H);

        foreach (var h in new List<IntPtr>(veils.Keys)) {
            if (wins.Contains(h)) continue;
            veils[h].Close(); veils[h].Dispose(); veils.Remove(h);
            Log("가림 제거 · 남은 가림 " + veils.Count);
        }

        IntPtr fg = Win.GetForegroundWindow();
        IntPtr fgRoot = fg == IntPtr.Zero ? IntPtr.Zero : Win.GetAncestor(fg, Win.GA_ROOTOWNER);
        Win.POINT cur; Win.GetCursorPos(out cur);
        bool ctrl = (Win.GetAsyncKeyState(Win.VK_CONTROL) & 0x8000) != 0;

        // 주인 창과 딸린 창은 한 묶음으로 다룬다. 쓰는 중이거나 엿보는 중이면 묶음 전체를 걷는다.
        var rects = new Dictionary<IntPtr, Rectangle>();
        var roots = new Dictionary<IntPtr, IntPtr>();
        var peekRoots = new HashSet<IntPtr>();
        foreach (var h in wins) {
            Win.RECT r = FrameOf(h);
            var rect = new Rectangle(r.L, r.T, r.R - r.L, r.B - r.T);
            IntPtr root = Win.GetAncestor(h, Win.GA_ROOTOWNER);
            if (root == IntPtr.Zero) root = h;
            rects[h] = rect; roots[h] = root;
            if (ctrl && rect.Contains(cur.X, cur.Y)) peekRoots.Add(root);
        }

        foreach (var h in wins) {
            Veil v;
            if (!veils.TryGetValue(h, out v)) {
                v = new Veil(h); veils[h] = v;
                Log("가림 생성 · " + (v.Owned ? "딸린 창" : "주인 창") + " · 가림 " + veils.Count);
            }
            var rect = rects[h];
            bool active = fg == h || (fgRoot != IntPtr.Zero && roots[h] == fgRoot);
            bool peek = peekRoots.Contains(roots[h]);
            string state = active ? "걷힘(사용 중)" : peek ? "걷힘(엿보기)" : "덮임";
            if (state != v.State) { Log((v.Owned ? "딸린 창 " : "주인 창 ") + state); v.State = state; }

            if (active || peek) {
                if (v.Visible) v.Hide();
                continue;
            }
            if (!v.Visible) { v.Show(); v.Last = Rectangle.Empty; }

            // 가림은 항상 대상 창 「바로 위」 에만 둔다. 맨 위로 올리면 대상 앞에 있는 다른 창까지 덮는다.
            IntPtr prev = Win.GetWindow(h, Win.GW_HWNDPREV);
            if (prev != v.Handle || rect != v.Last) {
                Win.SetWindowPos(v.Handle, prev, rect.X, rect.Y, rect.Width, rect.Height, Win.SWP_NOACTIVATE);
                v.Last = rect;
            }
        }
    }

    // 사람이 쓰는 창만 고른다. 숨은 창, 가상 데스크톱에 숨겨진 창(cloak), 너무 작은 창, 바탕화면·작업 표시줄은 뺀다.
    static List<WinInfo> EnumAppWindows() {
        var list = new List<WinInfo>();
        var cls = new StringBuilder(64);
        Win.EnumWindows((h, l) => {
            if (!Win.IsWindowVisible(h)) return true;
            uint pid; Win.GetWindowThreadProcessId(h, out pid);
            if (pid == selfPid) return true;
            int cloaked; Win.DwmGetWindowAttribute(h, Win.DWMWA_CLOAKED, out cloaked, 4);
            if (cloaked != 0) return true;
            cls.Length = 0; Win.GetClassName(h, cls, cls.Capacity);
            if (ShellClasses.Contains(cls.ToString())) return true;
            bool min = Win.IsIconic(h);
            if (!min) {
                Win.RECT r = FrameOf(h);
                if (r.R - r.L < 120 || r.B - r.T < 80) return true;
            }
            list.Add(new WinInfo { H = h, Pid = pid, Minimized = min });
            return true;
        }, IntPtr.Zero);
        return list;
    }

    static string NameOf(uint pid) {
        string n;
        if (pidNames.TryGetValue(pid, out n)) return n;
        try { using (var p = Process.GetProcessById((int)pid)) n = p.ProcessName; }
        catch (ArgumentException) { n = null; }
        catch (InvalidOperationException) { n = null; }
        pidNames[pid] = n;
        return n;
    }

    static string PathOf(uint pid) {
        IntPtr h = Win.OpenProcess(Win.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try {
            var sb = new StringBuilder(1024); int size = sb.Capacity;
            return Win.QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        } finally { Win.CloseHandle(h); }
    }

    // 보이는 테두리 기준 크기. GetWindowRect 는 눈에 안 보이는 크기 조절 여백까지 포함한다.
    static Win.RECT FrameOf(IntPtr h) {
        Win.RECT r;
        if (Win.DwmGetWindowAttribute(h, Win.DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(Win.RECT))) != 0)
            Win.GetWindowRect(h, out r);
        return r;
    }

    static void Quit(string why) {
        timer.Stop();
        foreach (var v in veils.Values) { v.Close(); v.Dispose(); }
        veils.Clear();
        tray.Visible = false; tray.Dispose();
        Log("끝 · " + why);
        Application.ExitThread();
    }

    // 창 제목은 남기지 않는다. 대화방 이름 같은 개인정보가 들어 있다.
    static void Log(string msg) {
        try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine, Encoding.UTF8); }
        catch (IOException) { }
    }
}
