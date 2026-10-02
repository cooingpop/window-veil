using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// 창 가림.
// 트레이에 상주하며, 고른 프로그램의 창을 따라다니다가 그 창을 쓰지 않을 때만 덮는다.
// 덮인 창 위에 마우스를 올리고 Ctrl 을 누르고 있으면 잠깐 보인다.
static class Win {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
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
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
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
    public const uint GA_ROOT = 2;
    public const uint GA_ROOTOWNER = 3;

    // 구독하는 창 변화 알림
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A, EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016, EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_OBJECT_CREATE = 0x8000, EVENT_OBJECT_REORDER = 0x8004;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const int VK_CONTROL = 0x11;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
}

// ── 가림 모양 ───────────────────────────────────────────────
// 흐림 말고는 가림 위에 다른 프로그램처럼 보이는 화면을 그린다. 가려져 있다는 것 자체를 알아채기 어렵게.
// 창 영역만 바뀐다. 작업 표시줄과 Alt+Tab 에는 원래 프로그램이 그대로 보인다.
public static class Skins {
    public static readonly string[][] All = {
        new[] { "blur", "흐림 (기본)" },
        new[] { "terminal", "빈 터미널" },
        new[] { "terminal-log", "로그가 올라가는 터미널" },
        new[] { "notepad", "빈 메모장" },
        new[] { "sheet", "빈 표" },
    };
    public const string ImagePrefix = "image:";
    static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
    static readonly Dictionary<string, Image> images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

    public static bool IsBlur(string key) { return key == null || key == "blur"; }
    public static bool IsLog(string key) { return key == "terminal-log"; }
    public static bool IsImage(string key) { return key != null && key.StartsWith(ImagePrefix, StringComparison.Ordinal); }

    public static bool IsValidKey(string key) {
        if (key == null) return false;
        if (All.Any(s => s[0] == key)) return true;
        if (!IsImage(key)) return false;
        string path = key.Substring(ImagePrefix.Length);
        return path.Length > 3 && path.Length <= 260 && path.All(c => !char.IsControl(c) && c != '|')
            && Path.IsPathRooted(path) && ImageExt.Contains(Path.GetExtension(path).ToLowerInvariant());
    }

    public static Color Back(string key) {
        if (key == "notepad" || key == "sheet") return Color.White;
        if (key == "terminal" || key == "terminal-log") return Color.FromArgb(12, 12, 12);
        return Color.Black;
    }

    public static Image ImageOf(string path) {
        Image img;
        if (images.TryGetValue(path, out img)) return img;
        try {
            // 파일을 잠그지 않게 메모리로 읽어 복사본을 만든다.
            using (var ms = new MemoryStream(File.ReadAllBytes(path)))
            using (var raw = Image.FromStream(ms)) img = new Bitmap(raw);
        } catch (IOException) { img = null; }
        catch (UnauthorizedAccessException) { img = null; }
        catch (ArgumentException) { img = null; }
        images[path] = img;
        return img;
    }

    public static void Draw(Graphics g, string key, Size z, float s, List<string> log) {
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        if (key == "terminal") Terminal(g, z, s, null);
        else if (key == "terminal-log") Terminal(g, z, s, log);
        else if (key == "notepad") Notepad(g, z, s);
        else if (key == "sheet") Sheet(g, z, s);
        else if (IsImage(key)) Picture(g, z, key.Substring(ImagePrefix.Length));
    }

    static readonly string[] LogTemplates = {
        "INFO  http       GET /health 200 {0}ms",
        "INFO  worker-{1}   job {2} done in {3}ms",
        "DEBUG cache      hit ratio 0.{4}",
        "INFO  scheduler  next run in {5}s",
        "INFO  http       POST /api/v1/events 202 {0}ms",
        "DEBUG gc         pause {7}ms",
        "WARN  pool       slow acquire {6}ms",
    };

    // 어느 회사·서비스와도 무관한 흔한 서버 로그처럼 보이는 줄.
    public static string NextLogLine(Random r) { return LogLineAt(r, DateTime.Now); }

    // 처음 띄울 때 채워 넣는 줄은 지난 시각으로 흩어 둔다. 모두 같은 시각이면 티가 난다.
    public static List<string> Backfill(Random r, int count) {
        var times = new List<DateTime>();
        var t = DateTime.Now;
        for (int i = 0; i < count; i++) { t = t.AddMilliseconds(-r.Next(400, 2600)); times.Add(t); }
        times.Reverse();
        return times.Select(x => LogLineAt(r, x)).ToList();
    }

    static string LogLineAt(Random r, DateTime when) {
        int i = r.Next(LogTemplates.Length);
        if (LogTemplates[i].StartsWith("WARN") && r.Next(4) != 0) i = 0;
        return "[" + when.ToString("HH:mm:ss.fff") + "] " + string.Format(LogTemplates[i],
            r.Next(1, 40), r.Next(1, 9), r.Next(0x100000, 0xFFFFFF).ToString("x6"), r.Next(20, 400),
            r.Next(80, 99), r.Next(5, 60), r.Next(300, 900), r.Next(2, 30));
    }

    static int P(float v, float s) { return (int)Math.Round(v * s); }

    static void Fill(Graphics g, Color c, int x, int y, int w, int h) {
        using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
    }

    static void Text(Graphics g, string t, Font f, Color c, RectangleF r, StringAlignment h) {
        using (var b = new SolidBrush(c))
        using (var sf = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(t, f, b, r, sf);
    }

    // 오른쪽 창 단추 세 개: 최소화 · 최대화 · 닫기
    static void Caption(Graphics g, Size z, float s, int barH, Color glyph) {
        int bw = P(46, s), cy = barH / 2, half = P(5, s);
        using (var pen = new Pen(glyph, Math.Max(1f, s))) {
            int x = z.Width - bw * 3 + bw / 2;
            g.DrawLine(pen, x - half, cy, x + half, cy);
            x += bw; g.DrawRectangle(pen, x - half, cy - half, half * 2, half * 2);
            x += bw; g.DrawLine(pen, x - half, cy - half, x + half, cy + half); g.DrawLine(pen, x - half, cy + half, x + half, cy - half);
        }
    }

    static void Terminal(Graphics g, Size z, float s, List<string> log) {
        int bar = P(40, s);
        Fill(g, Color.FromArgb(32, 32, 32), 0, 0, z.Width, bar);
        Fill(g, Color.FromArgb(12, 12, 12), 0, bar, z.Width, z.Height - bar);
        int tx = P(8, s), ty = P(8, s), tw = Math.Max(P(120, s), Math.Min(P(300, s), z.Width - P(260, s)));
        Fill(g, Color.FromArgb(12, 12, 12), tx, ty, tw, bar - ty);
        // 탭 아이콘: 파란 네모 안의 >
        int ic = P(16, s), ix = tx + P(12, s), iy = ty + (bar - ty - ic) / 2;
        Fill(g, Color.FromArgb(38, 113, 190), ix, iy, ic, ic);
        using (var pen = new Pen(Color.White, Math.Max(1f, 1.5f * s)))
            g.DrawLines(pen, new[] { new Point(ix + P(4, s), iy + P(4, s)), new Point(ix + P(8, s), iy + P(8, s)), new Point(ix + P(4, s), iy + P(12, s)) });
        using (var f = new Font("Segoe UI", 9f))
            Text(g, @"C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe", f, Color.FromArgb(230, 230, 230),
                new RectangleF(ix + ic + P(8, s), ty, tw - ic - P(64, s), bar - ty), StringAlignment.Near);
        using (var pen = new Pen(Color.FromArgb(200, 200, 200), Math.Max(1f, s))) {
            int cy = ty + (bar - ty) / 2, h = P(4, s);
            int cx = tx + tw - P(22, s);
            g.DrawLine(pen, cx - h, cy - h, cx + h, cy + h); g.DrawLine(pen, cx - h, cy + h, cx + h, cy - h);
            int px = tx + tw + P(22, s), ph = P(6, s);
            g.DrawLine(pen, px - ph, cy, px + ph, cy); g.DrawLine(pen, px, cy - ph, px, cy + ph);
            int vx = px + P(36, s);
            g.DrawLine(pen, vx - h, cy - h / 2, vx, cy + h / 2); g.DrawLine(pen, vx, cy + h / 2, vx + h, cy - h / 2);
        }
        Caption(g, z, s, bar, Color.FromArgb(220, 220, 220));
        if (log == null || log.Count == 0) return;
        using (var f = new Font("Consolas", 10f))
        using (var normal = new SolidBrush(Color.FromArgb(204, 204, 204)))
        using (var warn = new SolidBrush(Color.FromArgb(249, 241, 165))) {
            float lh = f.GetHeight(g);
            int top = bar + P(8, s), x = P(10, s);
            int fit = Math.Max(0, (int)((z.Height - top - P(8, s)) / lh));
            int start = Math.Max(0, log.Count - fit);
            for (int i = start; i < log.Count; i++)
                g.DrawString(log[i], f, log[i].Contains(" WARN ") ? warn : normal, x, top + (i - start) * lh);
        }
    }

    static void Notepad(Graphics g, Size z, float s) {
        int bar = P(32, s), menuH = P(26, s), status = P(26, s);
        var chrome = Color.FromArgb(243, 243, 243);
        Fill(g, chrome, 0, 0, z.Width, bar + menuH);
        Fill(g, Color.White, 0, bar + menuH, z.Width, Math.Max(0, z.Height - bar - menuH - status));
        Fill(g, chrome, 0, z.Height - status, z.Width, status);
        using (var line = new Pen(Color.FromArgb(225, 225, 225))) {
            g.DrawLine(line, 0, bar + menuH, z.Width, bar + menuH);
            g.DrawLine(line, 0, z.Height - status, z.Width, z.Height - status);
        }
        int ic = P(16, s), ix = P(12, s), iy = (bar - ic) / 2;
        Fill(g, Color.FromArgb(0, 120, 212), ix, iy, ic, ic);
        using (var pen = new Pen(Color.White, Math.Max(1f, s)))
            for (int k = 1; k <= 3; k++) g.DrawLine(pen, ix + P(3, s), iy + P(4 * k, s), ix + ic - P(3, s), iy + P(4 * k, s));
        using (var f = new Font("Malgun Gothic", 9f)) {
            Text(g, "제목 없음 - 메모장", f, Color.Black, new RectangleF(ix + ic + P(10, s), 0, Math.Max(0, z.Width - P(200, s)), bar), StringAlignment.Near);
            float mx = P(10, s);
            foreach (var m in new[] { "파일", "편집", "서식", "보기", "도움말" }) {
                Text(g, m, f, Color.Black, new RectangleF(mx, bar, P(60, s), menuH), StringAlignment.Near);
                mx += g.MeasureString(m, f).Width + P(16, s);
            }
            Text(g, "줄 1, 열 1", f, Color.FromArgb(60, 60, 60), new RectangleF(P(12, s), z.Height - status, P(160, s), status), StringAlignment.Near);
            Text(g, "100%      Windows (CRLF)      UTF-8", f, Color.FromArgb(60, 60, 60),
                new RectangleF(0, z.Height - status, z.Width - P(16, s), status), StringAlignment.Far);
        }
        Caption(g, z, s, bar, Color.FromArgb(30, 30, 30));
        using (var pen = new Pen(Color.Black, Math.Max(1f, s)))
            g.DrawLine(pen, P(8, s), bar + menuH + P(8, s), P(8, s), bar + menuH + P(26, s));
    }

    static void Sheet(Graphics g, Size z, float s) {
        int bar = P(32, s), tool = P(34, s), head = P(22, s), rowW = P(42, s), colW = P(72, s), rowH = P(22, s);
        Fill(g, Color.FromArgb(31, 110, 67), 0, 0, z.Width, bar);
        Fill(g, Color.FromArgb(243, 243, 243), 0, bar, z.Width, tool);
        int top = bar + tool;
        Fill(g, Color.White, 0, top, z.Width, Math.Max(0, z.Height - top));
        var headBg = Color.FromArgb(232, 232, 232);
        Fill(g, headBg, 0, top, z.Width, head);
        Fill(g, headBg, 0, top, rowW, Math.Max(0, z.Height - top));
        using (var f = new Font("Malgun Gothic", 9f)) {
            Text(g, "통합 문서1 - 표", f, Color.White, new RectangleF(P(14, s), 0, Math.Max(0, z.Width - P(200, s)), bar), StringAlignment.Near);
            Fill(g, Color.White, P(8, s), bar + P(6, s), P(64, s), tool - P(12, s));
            Text(g, "A1", f, Color.Black, new RectangleF(P(14, s), bar, P(56, s), tool), StringAlignment.Near);
            Text(g, "fx", f, Color.FromArgb(90, 90, 90), new RectangleF(P(84, s), bar, P(30, s), tool), StringAlignment.Near);
            Fill(g, Color.White, P(112, s), bar + P(6, s), Math.Max(0, z.Width - P(124, s)), tool - P(12, s));
            using (var grid = new Pen(Color.FromArgb(218, 218, 218))) {
                for (int x = rowW; x < z.Width; x += colW) g.DrawLine(grid, x, top, x, z.Height);
                for (int y = top + head; y < z.Height; y += rowH) g.DrawLine(grid, 0, y, z.Width, y);
            }
            for (int i = 0, x = rowW; x < z.Width; i++, x += colW)
                Text(g, ColName(i), f, Color.FromArgb(70, 70, 70), new RectangleF(x, top, colW, head), StringAlignment.Center);
            for (int i = 1, y = top + head; y < z.Height; i++, y += rowH)
                Text(g, i.ToString(), f, Color.FromArgb(70, 70, 70), new RectangleF(0, y, rowW, rowH), StringAlignment.Center);
        }
        using (var sel = new Pen(Color.FromArgb(16, 124, 65), Math.Max(2f, 2f * s)))
            g.DrawRectangle(sel, rowW, top + head, colW, rowH);
        Caption(g, z, s, bar, Color.White);
    }

    static string ColName(int i) {
        string n = ""; i++;
        while (i > 0) { int m = (i - 1) % 26; n = (char)('A' + m) + n; i = (i - 1) / 26; }
        return n;
    }

    // 비율을 지키며 창을 꽉 채우고, 넘치는 쪽은 잘라 낸다.
    static void Picture(Graphics g, Size z, string path) {
        var img = ImageOf(path);
        if (img == null) { Fill(g, Color.FromArgb(24, 24, 24), 0, 0, z.Width, z.Height); return; }
        float k = Math.Max((float)z.Width / img.Width, (float)z.Height / img.Height);
        float w = img.Width * k, h = img.Height * k;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, (z.Width - w) / 2, (z.Height - h) / 2, w, h);
    }
}

// ── 보이게 둘 영역 ──────────────────────────────────────────
// 창의 가장 가까운 가장자리 기준으로 저장한다. 그래야 창 크기를 바꿔도 입력칸 같은 자리를 따라간다.
// 값은 저장할 때 배율의 px 그대로 두고 배율(Dpi)을 함께 적는다. 같은 배율이면 반올림 없이 정확히 그 자리로 돌아온다.
public class Area {
    public string Program;
    public HashSet<string> Kind;   // 영역을 그린 창의 구성 요소 종류들. 지금 창이 이걸 모두 가지고 있으면 같은 종류의 창으로 본다.
    public char HA; public int H1, H2;   // L: 왼쪽에서 H1, 폭 H2 · R: 오른쪽에서 H1, 폭 H2 · S: 왼쪽 H1, 오른쪽 H2
    public char VA; public int V1, V2;   // T · B · S 도 같은 방식
    public string Snap;            // 붙여 둔 구성 요소 종류. 없으면 null
    public int Dpi = 96;

    public Rectangle Resolve(Rectangle frame, float scale) {
        float k = scale * 96f / Dpi;
        int a = Px(H1, k), b = Px(H2, k), x, w, y, h;
        if (HA == 'L') { x = frame.Left + a; w = b; }
        else if (HA == 'R') { w = b; x = frame.Right - a - b; }
        else { x = frame.Left + a; w = frame.Width - a - b; }
        a = Px(V1, k); b = Px(V2, k);
        if (VA == 'T') { y = frame.Top + a; h = b; }
        else if (VA == 'B') { h = b; y = frame.Bottom - a - b; }
        else { y = frame.Top + a; h = frame.Height - a - b; }
        return new Rectangle(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    public static Area From(string program, HashSet<string> kind, Rectangle r, Rectangle frame, float scale, string snap) {
        var ar = new Area { Program = program, Kind = kind, Snap = snap, Dpi = Math.Max(48, (int)Math.Round(scale * 96)) };
        int left = Math.Max(0, r.Left - frame.Left), right = Math.Max(0, frame.Right - r.Right);
        int top = Math.Max(0, r.Top - frame.Top), bottom = Math.Max(0, frame.Bottom - r.Bottom);
        // 창 폭(높이)의 60% 를 넘으면 양쪽에 붙이고, 아니면 가까운 쪽에 붙인다.
        if (r.Width > frame.Width * 0.6) { ar.HA = 'S'; ar.H1 = left; ar.H2 = right; }
        else if (left <= right) { ar.HA = 'L'; ar.H1 = left; ar.H2 = r.Width; }
        else { ar.HA = 'R'; ar.H1 = right; ar.H2 = r.Width; }
        if (r.Height > frame.Height * 0.6) { ar.VA = 'S'; ar.V1 = top; ar.V2 = bottom; }
        else if (top <= bottom) { ar.VA = 'T'; ar.V1 = top; ar.V2 = r.Height; }
        else { ar.VA = 'B'; ar.V1 = bottom; ar.V2 = r.Height; }
        return ar;
    }

    static int Px(int v, float k) { return (int)Math.Round(v * k); }

    public string Serialize() {
        return string.Join("\t", new[] {
            Program, string.Join(",", Kind.OrderBy(k => k, StringComparer.Ordinal)),
            HA + "," + H1 + "," + H2, VA + "," + V1 + "," + V2, Snap ?? "", Dpi.ToString() });
    }

    public static Area Parse(string line) {
        var f = line.Split('\t');
        if (f.Length != 6 || !VeilApp.IsValidName(f[0])) return null;
        int dpi;
        if (!int.TryParse(f[5], out dpi) || dpi < 48 || dpi > 960) return null;
        var kind = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in f[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (!ValidClass(k)) return null;
            kind.Add(k);
        }
        char ha, va; int h1, h2, v1, v2;
        if (!Anchor(f[2], "LRS", out ha, out h1, out h2) || !Anchor(f[3], "TBS", out va, out v1, out v2)) return null;
        string snap = f[4].Length == 0 ? null : f[4];
        if (snap != null && !ValidClass(snap)) return null;
        return new Area { Program = f[0], Kind = kind, HA = ha, H1 = h1, H2 = h2, VA = va, V1 = v1, V2 = v2, Snap = snap, Dpi = dpi };
    }

    static bool Anchor(string s, string letters, out char c, out int a, out int b) {
        c = '\0'; a = 0; b = 0;
        var p = s.Split(',');
        if (p.Length != 3 || p[0].Length != 1 || letters.IndexOf(p[0][0]) < 0) return false;
        c = p[0][0];
        return int.TryParse(p[1], out a) && int.TryParse(p[2], out b) && a >= 0 && b >= 0 && a <= 20000 && b <= 20000;
    }

    public static bool ValidClass(string k) {
        return k.Length > 0 && k.Length <= 128 && k.All(ch => !char.IsControl(ch) && ch != ',' && ch != '\t');
    }
}

// 가림 조각 하나. 창 하나를 덮을 때 보통 한 장이고, 보이게 둘 영역이 있으면 그 둘레를 여러 장으로 나눠 덮는다.
// 창 모양을 잘라 구멍을 내면 아크릴 흐림이 잘린 자리까지 칠해져서, 조각을 나누는 방식을 쓴다.
class Veil : Form {
    public readonly IntPtr Target;
    public Rectangle Last { get; set; } // 필드로 두면 Form(참조로 마샬링)의 필드 멤버 접근 경고가 난다
    readonly VeilGroup group;
    readonly bool blur;

    public Veil(VeilGroup group) {
        this.group = group;
        Target = group.Target;
        blur = Skins.IsBlur(group.Skin);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = blur ? Color.Black : Skins.Back(group.Skin);
        if (!blur) SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Bounds = new Rectangle(-32000, -32000, 10, 10);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    // 포커스를 뺏지 않고(NOACTIVATE), 작업 표시줄과 Alt+Tab 에 안 뜨게(TOOLWINDOW) 한다.
    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; }
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        if (!blur) return;
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

    // 흐림일 때는 아무것도 그리지 않는다. 다른 모양일 때는 창 전체 그림 가운데 이 조각에 해당하는 부분만 그린다.
    // 딸린 창(카카오톡 하단 띠 등)은 주인 창의 그림을 이어서 그려 한 화면처럼 보이게 한다.
    protected override void OnPaint(PaintEventArgs e) {
        if (blur) return;
        var src = group.Owner ?? group;
        if (src.Frame.IsEmpty) return;
        e.Graphics.TranslateTransform(src.Frame.Left - Left, src.Frame.Top - Top);
        Skins.Draw(e.Graphics, src.Skin, src.Frame.Size, src.Scale, src.Log);
    }
}

// 창 하나에 붙는 가림 조각 묶음.
class VeilGroup {
    public readonly IntPtr Target;
    public readonly bool Owned; // 딸린 창(카카오톡 하단 광고 띠 등)인지
    public readonly List<Veil> Pieces = new List<Veil>();
    public VeilGroup Owner;     // 딸린 창이면 주인 창의 묶음
    public string State = "";
    public string Skin = "blur";
    public Rectangle Frame = Rectangle.Empty;
    public float Scale = 1f;
    public int HoleCount = -1;
    public HashSet<string> Kind;
    public DateTime KindAt = DateTime.MinValue;
    public IntPtr Root;         // 주인 창(딸린 창이 아니면 자기 자신)
    public List<string> Log;
    public int LogSerial;
    public DateTime NextLogAt = DateTime.MinValue;
    public string LastPaintKey;

    public VeilGroup(IntPtr target) {
        Target = target;
        Owned = Win.GetWindow(target, Win.GW_OWNER) != IntPtr.Zero;
    }

    public void Resize(int count) {
        while (Pieces.Count < count) Pieces.Add(new Veil(this));
        while (Pieces.Count > count) {
            var v = Pieces[Pieces.Count - 1];
            Pieces.RemoveAt(Pieces.Count - 1);
            v.Close(); v.Dispose();
        }
        LastPaintKey = null;
    }

    public void HideAll() { foreach (var v in Pieces) if (v.Visible) v.Hide(); }

    public void Dispose() { Resize(0); }
}

// ── 영역 조절 화면 ───────────────────────────────────────────

// 보이게 둘 영역을 나타내는 파란 사각형. 일반 창처럼 가운데를 끌면 옮겨지고 가장자리를 끌면 크기가 바뀐다.
class AreaBox : Form {
    public string Snap;
    public bool IsChosen;
    public event Action<AreaBox> Released;
    public event Action<AreaBox> Chosen;

    public AreaBox(Rectangle r) {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(0, 120, 215);
        Opacity = 0.45;
        MinimumSize = new Size(24, 16);
        Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Bounds = r;
    }

    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x00000080; return cp; }
    }

    protected override void OnActivated(EventArgs e) { base.OnActivated(e); if (Chosen != null) Chosen(this); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

    protected override void WndProc(ref Message m) {
        const int WM_NCHITTEST = 0x84, WM_EXITSIZEMOVE = 0x232;
        if (m.Msg == WM_NCHITTEST) {
            long lp = m.LParam.ToInt64();
            var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            int g = 10;
            bool l = p.X < g, r = p.X >= Width - g, t = p.Y < g, b = p.Y >= Height - g;
            // 가장자리는 크기 조절, 나머지는 제목 표시줄처럼 끌어 옮기기
            int ht = t && l ? 13 : t && r ? 14 : b && l ? 16 : b && r ? 17 : l ? 10 : r ? 11 : t ? 12 : b ? 15 : 2;
            m.Result = (IntPtr)ht;
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == WM_EXITSIZEMOVE && Released != null) Released(this);
    }

    protected override void OnPaint(PaintEventArgs e) {
        using (var pen = new Pen(IsChosen ? Color.Gold : Color.White, 3)) e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        string label = Snap != null ? "보이게 둘 영역 · 구성 요소에 붙음" : "보이게 둘 영역";
        TextRenderer.DrawText(e.Graphics, label, Font, new Point(8, 6), Color.White);
    }
}

// 영역 조절 도구 창. 사각형 옆에 떠서 추가 · 지우기 · 저장을 한다.
class AreaEditor : Form {
    readonly IntPtr target;
    readonly Rectangle frame;
    readonly List<AreaBox> boxes = new List<AreaBox>();
    AreaBox chosen;
    public Action<List<KeyValuePair<Rectangle, string>>> OnSave;

    public AreaEditor(IntPtr target, string display, Rectangle frame, List<KeyValuePair<Rectangle, string>> existing) {
        this.target = target;
        this.frame = frame;
        Text = "보이게 둘 영역 조절 · " + display;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Malgun Gothic", 9.5f);

        int width;
        using (var g = CreateGraphics()) width = (int)(360 * g.DpiX / 96f);
        var flow = new FlowLayoutPanel {
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12, 8, 12, 10)
        };
        flow.Controls.Add(new Label {
            AutoSize = true, MaximumSize = new Size(width, 0),
            Text = "파란 사각형 안은 창이 가려져 있어도 보입니다.\r\n" +
                   "가운데를 끌면 옮겨지고, 가장자리나 모서리를 끌면 크기가 바뀝니다.\r\n" +
                   "입력칸 같은 구성 요소 가까이 놓으면 그 크기에 딱 맞게 붙고, 그 뒤로는 그 구성 요소를 따라갑니다.\r\n" +
                   "저장하면 이 프로그램의 같은 종류 창에 모두 적용됩니다."
        });
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
        var add = Btn("영역 하나 더");
        add.Click += (s, e) => AddBox(DefaultRect(boxes.Count), null);
        var del = Btn("고른 영역 지우기");
        del.Click += (s, e) => RemoveChosen();
        var save = Btn("저장하고 닫기");
        save.Click += (s, e) => {
            if (OnSave != null) OnSave(boxes.Select(b => new KeyValuePair<Rectangle, string>(b.Bounds, b.Snap)).ToList());
            Close();
        };
        var cancel = Btn("저장하지 않고 닫기");
        cancel.Click += (s, e) => Close();
        row.Controls.AddRange(new Control[] { add, del, save, cancel });
        flow.Controls.Add(row);
        Controls.Add(flow);
        CancelButton = cancel;

        foreach (var kv in existing) AddBox(kv.Key, kv.Value);
        if (boxes.Count == 0) AddBox(DefaultRect(0), null);
    }

    static Button Btn(string t) { return new Button { Text = t, AutoSize = true, Margin = new Padding(0, 0, 6, 0) }; }

    Rectangle DefaultRect(int n) {
        int off = n * 18;
        return new Rectangle(frame.Left + frame.Width / 4 + off, frame.Top + frame.Height * 2 / 5 + off, frame.Width / 2, Math.Max(24, frame.Height / 5));
    }

    // 도구 창은 대상 창 바로 위(공간이 없으면 아래, 그래도 없으면 화면 안쪽)에 둔다.
    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        var wa = Screen.FromRectangle(frame).WorkingArea;
        int x = Math.Max(wa.Left, Math.Min(frame.Right - Width, wa.Right - Width));
        int y = frame.Top - Height - 8;
        if (y < wa.Top) y = frame.Bottom + 8;
        if (y + Height > wa.Bottom) y = Math.Max(wa.Top, wa.Bottom - Height);
        Location = new Point(x, y);
    }

    void AddBox(Rectangle r, string snap) {
        var b = new AreaBox(r) { Snap = snap };
        b.Released += SnapBox;
        b.Chosen += Choose;
        boxes.Add(b);
        b.Show();
        Choose(b);
    }

    void Choose(AreaBox b) {
        foreach (var x in boxes) {
            bool c = x == b;
            if (x.IsChosen != c) { x.IsChosen = c; x.Invalidate(); }
        }
        chosen = b;
    }

    void RemoveChosen() {
        var b = chosen ?? boxes.LastOrDefault();
        if (b == null) return;
        boxes.Remove(b);
        b.Close(); b.Dispose();
        chosen = null;
        if (boxes.Count > 0) Choose(boxes[boxes.Count - 1]);
    }

    // 끌기를 마치면 창 밖으로 나간 부분을 잘라 내고, 가까운 구성 요소가 있으면 거기에 딱 맞춘다.
    void SnapBox(AreaBox b) {
        var live = Win.IsWindow(target) ? VeilApp.FrameRect(target) : frame;
        var r = Rectangle.Intersect(b.Bounds, live);
        if (r.Width < 24 || r.Height < 16) r = b.Bounds;
        Rectangle snapped; string cls;
        if (VeilApp.FindSnap(target, r, out snapped, out cls)) { b.Bounds = snapped; b.Snap = cls; }
        else { b.Bounds = r; b.Snap = null; }
        b.Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        foreach (var b in boxes) { b.Close(); b.Dispose(); }
        boxes.Clear();
        base.OnFormClosed(e);
    }
}

// 방패 아이콘 메뉴의 「사용법 보기」. 할 일별로 묶어 보여준다.
class HelpForm : Form {
    static readonly string[][] Sections = {
        new[] { "프로그램 가리기", "작업 표시줄의 방패 아이콘을 누르고, 목록에서 가릴 프로그램을 체크합니다. 체크한 프로그램은 다른 창을 쓰는 동안 덮입니다. 메뉴가 닫히지 않으니 여러 개를 이어서 체크할 수 있습니다." },
        new[] { "잠깐 보기", "덮인 창 위에 마우스를 올리고 Ctrl 을 누르고 있으면 보입니다. 손을 떼면 다시 덮입니다." },
        new[] { "그 창으로 돌아가기", "덮인 창을 클릭하면 그 창으로 넘어가고 가림이 걷힙니다. 작업 표시줄이나 Alt+Tab 으로 넘어가도 같습니다." },
        new[] { "가림 모양 바꾸기", "메뉴의 「가린 창 꾸미기」 에서 프로그램을 고르면, 흐림 대신 빈 터미널, 로그가 올라가는 터미널, 빈 메모장, 빈 표, 직접 고른 그림으로 덮을 수 있습니다. 가려져 있다는 것 자체를 알아채기 어렵게 하려는 것입니다. 작업 표시줄과 Alt+Tab 에는 원래 프로그램이 그대로 보입니다." },
        new[] { "일부만 보이게 두기", "같은 곳에서 「보이게 둘 영역 조절하기」 를 누르고 그 프로그램 창을 클릭하면, 창 위에 파란 사각형이 뜹니다. 가운데를 끌면 옮겨지고 가장자리를 끌면 크기가 바뀝니다. 입력칸 같은 구성 요소 가까이 놓으면 딱 맞게 붙고, 그 뒤로는 그 구성 요소를 따라갑니다. 저장하면 같은 종류의 창에 모두 적용됩니다. 영역 안의 내용은 지나가는 사람에게도 보입니다." },
        new[] { "가림 그만두기", "목록에서 체크를 풀면 그 프로그램만 그만 가립니다. 전부 잠깐 멈추려면 「모든 가림 잠시 끄기」, 프로그램을 닫으려면 「창 가림 끝내기」 를 누릅니다." },
        new[] { "저장하는 것", "고른 프로그램 이름, 가림 모양(그림을 골랐다면 그 파일 위치), 보이게 둘 영역의 위치만 %APPDATA%\\WindowVeil 에 저장합니다. 창 제목이나 화면 내용은 저장하지 않습니다." },
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
        using (var g = CreateGraphics()) width = (int)(460 * g.DpiX / 96f);
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

    // targets.txt 한 줄은 「프로그램 이름」 또는 「프로그램 이름|skin=모양」.
    const string SkinFlag = "skin=";

    static string logPath;
    static string settingsPath;
    static string areasPath;
    static uint selfPid;
    static DateTime endAt = DateTime.MaxValue;
    static bool enabled = true;
    static DateTime lastFull = DateTime.MinValue;
    static DateTime lastPidClear = DateTime.Now;
    static IntPtr lastFg = IntPtr.Zero;
    static IntPtr lastPeekRoot = IntPtr.Zero;
    static readonly Random rng = new Random();
    static readonly HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> skins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly List<Area> areas = new List<Area>();
    static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<uint, string> pidNames = new Dictionary<uint, string>();
    static readonly Dictionary<IntPtr, VeilGroup> groups = new Dictionary<IntPtr, VeilGroup>();
    static NotifyIcon tray;
    static ContextMenuStrip menu;
    static ToolStripMenuItem statusItem;
    static HelpForm helpForm;
    static AreaEditor editor;
    static IntPtr editingTarget = IntPtr.Zero;
    static string pendingEdit;
    static DateTime pendingUntil;
    // 가벼운 확인(80ms)과, 다시 계산이 필요할 때만 잠깐 켜는 타이머. 알림이 몰려도 한 번에 묶어 계산한다.
    static System.Windows.Forms.Timer pollTimer, updateTimer;
    static Win.WinEventProc hookProc; // 대리자가 수거되면 알림이 끊기므로 붙잡아 둔다
    static readonly List<IntPtr> hooks = new List<IntPtr>();
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
        areasPath = Path.Combine(dir, "areas.txt");
        bool firstRun = !File.Exists(settingsPath);
        LoadSelection();
        LoadAreas();

        Application.EnableVisualStyles();
        menu = new ContextMenuStrip();
        menu.Opening += (s, e) => { BuildMenu(); e.Cancel = false; };
        menu.ItemClicked += (s, e) => { keepMenuOpen = e.ClickedItem.Tag is string; };
        // 체크 항목이나 하위 메뉴가 있는 항목을 누를 때는 메뉴를 닫지 않는다.
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
            Balloon("창 가림이 켜졌습니다", "작업 표시줄의 방패 아이콘을 눌러 가릴 프로그램을 체크하세요. 사용법도 그 메뉴에 있습니다.");
        else
            Balloon("창 가림이 켜졌습니다", selected.Count + "개 프로그램을 가립니다. 바꾸려면 방패 아이콘을 누르세요.");

        updateTimer = new System.Windows.Forms.Timer { Interval = 15 };
        updateTimer.Tick += (s, e) => { updateTimer.Stop(); Update(); };
        pollTimer = new System.Windows.Forms.Timer { Interval = 80 };
        pollTimer.Tick += (s, e) => Poll();
        pollTimer.Start();
        Subscribe();
        MarkDirty();
        Log("시작 · 고른 프로그램 " + selected.Count + "개 · 보이게 둘 영역 " + areas.Count + "개" + (minutes > 0 ? " · " + minutes + "분 뒤 종료" : ""));
        Application.Run();
    }

    static void Balloon(string title, string text) { tray.ShowBalloonTip(5000, title, text, ToolTipIcon.Info); }

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

        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<KeyValuePair<string, ToolStripMenuItem>>();
        foreach (var name in running.Keys.Union(selected, StringComparer.OrdinalIgnoreCase)) {
            uint pid;
            string path = running.TryGetValue(name, out pid) ? PathOf(pid) : null;
            paths[name] = path;
            displayNames[name] = DisplayName(name, path);
            string label = displayNames[name] + (running.ContainsKey(name) ? "" : " · 지금 꺼져 있음");
            var item = new ToolStripMenuItem(label) { Tag = name, Checked = selected.Contains(name), Image = IconOf(path) };
            item.Click += (s, e) => Toggle((ToolStripMenuItem)s);
            rows.Add(new KeyValuePair<string, ToolStripMenuItem>(label, item));
        }
        if (rows.Count == 0)
            menu.Items.Add(new ToolStripMenuItem("지금 창이 떠 있는 프로그램이 없습니다") { Enabled = false });
        foreach (var row in rows.OrderBy(r => r.Key, StringComparer.CurrentCultureIgnoreCase))
            menu.Items.Add(row.Value);

        // 가리고 있는 프로그램마다 가림 모양과 보이게 둘 영역을 정한다.
        if (selected.Count > 0) {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("가린 창 꾸미기 · 가림 모양과 보이게 둘 영역을 정합니다") { Enabled = false });
            foreach (var name in selected.OrderBy(n => Shown(n), StringComparer.CurrentCultureIgnoreCase)) {
                string path; paths.TryGetValue(name, out path);
                var parent = new ToolStripMenuItem(Shown(name)) { Tag = "settings:" + name, Image = IconOf(path) };
                parent.DropDownItems.Add(new ToolStripMenuItem("가림 모양") { Enabled = false });
                string current = SkinOf(name);
                foreach (var sk in Skins.All) {
                    string key = sk[0], n2 = name;
                    var it = new ToolStripMenuItem(sk[1]) { Checked = current == key };
                    it.Click += (s, e) => SetSkin(n2, key);
                    parent.DropDownItems.Add(it);
                }
                string nameForImage = name;
                var img = new ToolStripMenuItem("그림 파일 고르기...") { Checked = Skins.IsImage(current) };
                img.Click += (s, e) => PickImage(nameForImage);
                parent.DropDownItems.Add(img);
                parent.DropDownItems.Add(new ToolStripSeparator());
                int count = areas.Count(a => a.Program.Equals(name, StringComparison.OrdinalIgnoreCase));
                string nameForEdit = name;
                var edit = new ToolStripMenuItem("보이게 둘 영역 조절하기" + (count > 0 ? " (지금 " + count + "개)" : ""));
                edit.Click += (s, e) => BeginEdit(nameForEdit);
                parent.DropDownItems.Add(edit);
                menu.Items.Add(parent);
            }
        }

        menu.Items.Add(new ToolStripSeparator());
        var help = new ToolStripMenuItem("사용법 보기");
        help.Click += (s, e) => ShowHelp();
        menu.Items.Add(help);
        var pause = new ToolStripMenuItem(enabled ? "모든 가림 잠시 끄기" : "모든 가림 다시 켜기");
        pause.Click += (s, e) => {
            enabled = !enabled;
            Log(enabled ? "가림 다시 켬" : "가림 잠시 끔");
            MarkDirty();
        };
        menu.Items.Add(pause);
        var quit = new ToolStripMenuItem("창 가림 끝내기");
        quit.Click += (s, e) => Quit("메뉴에서 끝냄");
        menu.Items.Add(quit);
    }

    static string Shown(string name) { string d; return displayNames.TryGetValue(name, out d) ? d : name; }

    static void Toggle(ToolStripMenuItem item) {
        string name = (string)item.Tag;
        if (selected.Contains(name)) selected.Remove(name); else selected.Add(name);
        item.Checked = selected.Contains(name);
        SaveSelection();
        if (statusItem != null) statusItem.Text = StatusText();
        Log((item.Checked ? "가림 대상 추가 · " : "가림 대상 해제 · ") + name);
        MarkDirty();
    }

    static string SkinOf(string name) { string k; return skins.TryGetValue(name, out k) ? k : "blur"; }

    static void SetSkin(string name, string key) {
        if (Skins.IsBlur(key)) skins.Remove(name); else skins[name] = key;
        SaveSelection();
        Log("가림 모양 · " + name + " · " + (Skins.IsImage(key) ? "그림 파일" : key));
        MarkDirty();
    }

    static void PickImage(string name) {
        using (var d = new OpenFileDialog { Title = "가림으로 쓸 그림 고르기 · " + Shown(name), Filter = "그림 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif" }) {
            if (d.ShowDialog() != DialogResult.OK) return;
            string key = Skins.ImagePrefix + d.FileName;
            if (Skins.IsValidKey(key) && Skins.ImageOf(d.FileName) != null) SetSkin(name, key);
            else Balloon("그림을 열 수 없습니다", "다른 그림 파일을 골라 주세요. png, jpg, bmp, gif 를 쓸 수 있습니다.");
        }
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

    // ── 영역 조절 ──────────────────────────────────────────

    // 그 프로그램 창이 하나면 바로 시작하고, 여럿이면 사용자가 클릭한 창으로 시작한다.
    static void BeginEdit(string program) {
        if (editor != null && !editor.IsDisposed) { editor.Activate(); return; }
        var cands = EnumAppWindows().Where(w => !w.Minimized && Win.GetWindow(w.H, Win.GW_OWNER) == IntPtr.Zero
            && program.Equals(NameOf(w.Pid), StringComparison.OrdinalIgnoreCase)).Select(w => w.H).ToList();
        if (cands.Count == 0) { Balloon("창이 보이지 않습니다", "먼저 " + Shown(program) + " 창을 열어 주세요."); return; }
        if (cands.Count == 1) { StartEditor(cands[0], program); return; }
        pendingEdit = program;
        pendingUntil = DateTime.Now.AddSeconds(30);
        Balloon("영역을 조절할 창을 클릭하세요", Shown(program) + " 창이 여러 개입니다. 30초 안에 조절할 창을 클릭하세요.");
    }

    static void StartEditor(IntPtr h, string program) {
        var frame = FrameRect(h); float scale = ScaleOf(h); var kind = KindOf(h);
        var existing = new List<KeyValuePair<Rectangle, string>>();
        foreach (var a in areas) {
            if (!a.Program.Equals(program, StringComparison.OrdinalIgnoreCase) || !a.Kind.IsSubsetOf(kind)) continue;
            var r = Rectangle.Intersect(ResolveArea(h, a, frame, scale), frame);
            if (r.Width < 8 || r.Height < 8) r = Rectangle.Intersect(a.Resolve(frame, scale), frame);
            if (r.Width >= 8 && r.Height >= 8) existing.Add(new KeyValuePair<Rectangle, string>(r, a.Snap));
        }
        editingTarget = h;
        editor = new AreaEditor(h, Shown(program), frame, existing);
        editor.OnSave = list => SaveEdit(h, program, list);
        editor.FormClosed += (s, e) => { editingTarget = IntPtr.Zero; editor = null; Log("영역 조절 닫음"); MarkDirty(); };
        editor.Show();
        Log("영역 조절 시작 · " + program + " · 기존 영역 " + existing.Count + "개");
    }

    static void SaveEdit(IntPtr h, string program, List<KeyValuePair<Rectangle, string>> list) {
        if (!Win.IsWindow(h)) return;
        var frame = FrameRect(h); float scale = ScaleOf(h); var kind = KindOf(h);
        areas.RemoveAll(a => a.Program.Equals(program, StringComparison.OrdinalIgnoreCase) && a.Kind.IsSubsetOf(kind));
        int n = 0;
        foreach (var kv in list) {
            var r = Rectangle.Intersect(kv.Key, frame);
            if (r.Width < 8 || r.Height < 8) continue;
            areas.Add(Area.From(program, kind, r, frame, scale, kv.Value));
            n++;
        }
        SaveAreas();
        Log("보이게 둘 영역 저장 · " + program + " · " + n + "개");
        MarkDirty();
    }

    // 끌어 놓은 사각형의 네 변이 모두 가까운 구성 요소를 찾는다. 창 거의 전체를 덮는 구성 요소는 뺀다.
    internal static bool FindSnap(IntPtr top, Rectangle r, out Rectangle snapped, out string cls) {
        snapped = Rectangle.Empty; cls = null;
        if (!Win.IsWindow(top)) return false;
        var frame = FrameRect(top);
        int tol = (int)Math.Round(14 * ScaleOf(top));
        int bestScore = int.MaxValue; Rectangle best = Rectangle.Empty; string bestCls = null;
        var sb = new StringBuilder(128);
        Win.EnumChildWindows(top, (h, l) => {
            if (!Win.IsWindowVisible(h)) return true;
            Win.RECT cr; Win.GetWindowRect(h, out cr);
            var c = Rectangle.Intersect(new Rectangle(cr.L, cr.T, cr.R - cr.L, cr.B - cr.T), frame);
            if (c.Width < 24 || c.Height < 16) return true;
            if ((long)c.Width * c.Height > (long)frame.Width * frame.Height * 7 / 10) return true;
            int dl = Math.Abs(c.Left - r.Left), dr = Math.Abs(c.Right - r.Right), dt = Math.Abs(c.Top - r.Top), db = Math.Abs(c.Bottom - r.Bottom);
            if (dl > tol || dr > tol || dt > tol || db > tol) return true;
            int score = dl + dr + dt + db;
            if (score < bestScore) {
                bestScore = score; best = c;
                sb.Length = 0; Win.GetClassName(h, sb, sb.Capacity); bestCls = sb.ToString();
            }
            return true;
        }, IntPtr.Zero);
        if (bestCls == null || !Area.ValidClass(bestCls)) return false;
        snapped = best; cls = bestCls;
        return true;
    }

    // 붙여 둔 구성 요소가 있으면 그 실제 위치를 쓴다. 저장한 자리와 가장 많이 겹치는 것을 고른다.
    // 그 구성 요소가 안 보이면 그 자리도 덮는다. 엉뚱한 곳이 드러나는 것보다 낫다.
    static Rectangle ResolveArea(IntPtr top, Area a, Rectangle frame, float scale) {
        var guess = a.Resolve(frame, scale);
        if (a.Snap == null) return guess;
        Rectangle best = Rectangle.Empty; long bestOverlap = 0;
        var sb = new StringBuilder(128);
        Win.EnumChildWindows(top, (h, l) => {
            if (!Win.IsWindowVisible(h)) return true;
            sb.Length = 0; Win.GetClassName(h, sb, sb.Capacity);
            if (sb.ToString() != a.Snap) return true;
            Win.RECT cr; Win.GetWindowRect(h, out cr);
            var c = new Rectangle(cr.L, cr.T, cr.R - cr.L, cr.B - cr.T);
            var x = Rectangle.Intersect(c, guess);
            long ov = x.Width > 0 && x.Height > 0 ? (long)x.Width * x.Height : 0;
            if (ov > bestOverlap) { bestOverlap = ov; best = c; }
            return true;
        }, IntPtr.Zero);
        return bestOverlap > 0 ? best : Rectangle.Empty;
    }

    // 창 안 구성 요소의 종류 이름 모음. 글자 내용은 읽지 않는다.
    static HashSet<string> KindOf(IntPtr top) {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder(128);
        Win.EnumChildWindows(top, (h, l) => {
            sb.Length = 0; Win.GetClassName(h, sb, sb.Capacity);
            string c = sb.ToString();
            if (Area.ValidClass(c)) set.Add(c);
            return true;
        }, IntPtr.Zero);
        return set;
    }

    // ── 저장 · 프로그램 이름, 가림 모양, 영역 위치만 저장한다. 창 제목은 남기지 않는다 ──

    static void LoadSelection() {
        selected.Clear(); skins.Clear();
        if (!File.Exists(settingsPath)) return;
        foreach (var line in File.ReadAllLines(settingsPath, Encoding.UTF8)) {
            var parts = line.Split('|');
            string n = parts[0].Trim();
            if (!IsValidName(n)) continue;
            selected.Add(n);
            foreach (var f in parts.Skip(1)) {
                string flag = f.Trim();
                if (flag.StartsWith(SkinFlag, StringComparison.Ordinal) && Skins.IsValidKey(flag.Substring(SkinFlag.Length)))
                    skins[n] = flag.Substring(SkinFlag.Length);
            }
        }
    }

    static void SaveSelection() {
        var lines = selected.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => Skins.IsBlur(SkinOf(n)) ? n : n + "|" + SkinFlag + SkinOf(n)).ToArray();
        try { File.WriteAllLines(settingsPath, lines, Encoding.UTF8); }
        catch (IOException ex) { Log("고른 목록 저장 실패 · " + ex.Message); }
    }

    static void LoadAreas() {
        areas.Clear();
        if (!File.Exists(areasPath)) return;
        foreach (var line in File.ReadAllLines(areasPath, Encoding.UTF8)) {
            var a = Area.Parse(line.TrimEnd('\r'));
            if (a != null) areas.Add(a);
        }
    }

    static void SaveAreas() {
        try { File.WriteAllLines(areasPath, areas.Select(a => a.Serialize()).ToArray(), Encoding.UTF8); }
        catch (IOException ex) { Log("영역 저장 실패 · " + ex.Message); }
    }

    internal static bool IsValidName(string n) {
        return n.Length > 0 && n.Length <= 100 && n.All(c => !char.IsControl(c) && c != '\\' && c != '/' && c != '|' && c != '\t');
    }

    // ── 언제 다시 계산할지 ──────────────────────────────────────
    // 예전에는 50ms 마다 화면의 모든 창을 훑었다(코어 하나 기준 27%). 지금은 Windows 의 창 변화 알림을 받을 때,
    // 맨 앞 창이나 엿보기 상태가 바뀔 때, 로그 터미널에 줄을 올릴 때만 계산한다. 알림이 빠질 때를 대비해 1초에 한 번은 계산한다.

    static void Subscribe() {
        hookProc = OnWinEvent;
        var ranges = new[] {
            new[] { Win.EVENT_SYSTEM_FOREGROUND, Win.EVENT_SYSTEM_FOREGROUND },
            new[] { Win.EVENT_SYSTEM_MOVESIZESTART, Win.EVENT_SYSTEM_MOVESIZEEND },
            new[] { Win.EVENT_SYSTEM_MINIMIZESTART, Win.EVENT_SYSTEM_MINIMIZEEND },
            new[] { Win.EVENT_OBJECT_CREATE, Win.EVENT_OBJECT_REORDER },          // 생김 · 없어짐 · 보임 · 숨음 · 순서 바뀜
            new[] { Win.EVENT_OBJECT_LOCATIONCHANGE, Win.EVENT_OBJECT_LOCATIONCHANGE },
            new[] { Win.EVENT_OBJECT_CLOAKED, Win.EVENT_OBJECT_UNCLOAKED },
        };
        // 자기 프로그램(가림 조각) 의 변화는 받지 않는다. 받으면 가림을 옮긴 일이 다시 알림이 되어 끝없이 돈다.
        foreach (var r in ranges) {
            IntPtr h = Win.SetWinEventHook(r[0], r[1], IntPtr.Zero, hookProc, 0, 0, Win.WINEVENT_OUTOFCONTEXT | Win.WINEVENT_SKIPOWNPROCESS);
            if (h != IntPtr.Zero) hooks.Add(h);
        }
        Log("창 변화 알림 구독 " + hooks.Count + "/" + ranges.Length);
    }

    static void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time) {
        if (ev == Win.EVENT_SYSTEM_FOREGROUND) { MarkDirty(); return; }
        // 창 자체의 변화만 본다. 커서·캐럿·스크롤 막대 같은 창 안 물체의 변화는 무시한다.
        if (hwnd == IntPtr.Zero || idObject != 0 || idChild != 0) return;
        IntPtr root = Win.GetAncestor(hwnd, Win.GA_ROOT);
        if (root == IntPtr.Zero) root = hwnd;
        // 가리고 있는 창이나 그 안의 구성 요소(입력칸이 커지는 등)가 바뀌었으면 다시 계산한다.
        if (groups.ContainsKey(root)) { MarkDirty(); return; }
        // 아직 안 가린 창이라도, 고른 프로그램의 창이 새로 뜨거나 보이게 되면 다시 계산한다.
        if (ev == Win.EVENT_OBJECT_LOCATIONCHANGE || root != hwnd) return;
        uint pid; Win.GetWindowThreadProcessId(hwnd, out pid);
        string n = NameOf(pid);
        if (n != null && (selected.Contains(n) || n.Equals(pendingEdit, StringComparison.OrdinalIgnoreCase))) MarkDirty();
    }

    static void MarkDirty() { if (updateTimer != null && !updateTimer.Enabled) updateTimer.Start(); }

    // 80ms 마다 하는 가벼운 확인. 모든 창을 훑지 않는다.
    static void Poll() {
        var now = DateTime.Now;
        if (now > endAt) { Quit("시간이 다 되어 끝냄"); return; }
        IntPtr fg = Win.GetForegroundWindow();
        if (fg != lastFg) { lastFg = fg; MarkDirty(); }

        // 엿보기: Ctrl 을 누른 채 덮인 창 위에 마우스가 있는지
        IntPtr peekRoot = IntPtr.Zero;
        if ((Win.GetAsyncKeyState(Win.VK_CONTROL) & 0x8000) != 0) {
            Win.POINT cur; Win.GetCursorPos(out cur);
            foreach (var g in groups.Values)
                if (g.Frame.Contains(cur.X, cur.Y)) { peekRoot = g.Root; break; }
        }
        if (peekRoot != lastPeekRoot) { lastPeekRoot = peekRoot; MarkDirty(); }

        foreach (var g in groups.Values)
            if (g.Owner == null && Skins.IsLog(g.Skin) && now >= g.NextLogAt) { MarkDirty(); break; }

        if ((now - lastFull).TotalMilliseconds >= 1000) MarkDirty();
    }

    // ── 창 따라가기 ──────────────────────────────────────────

    static void Update() {
        var now = DateTime.Now;
        lastFull = now;
        // 끝난 프로세스 번호가 다른 프로그램에 재사용될 수 있어 5초마다 비운다.
        if ((now - lastPidClear).TotalSeconds >= 5) { pidNames.Clear(); lastPidClear = now; }

        if (editor != null && (!Win.IsWindow(editingTarget) || Win.IsIconic(editingTarget) || !Win.IsWindowVisible(editingTarget)))
            editor.Close();

        var wins = new List<IntPtr>();
        var winNames = new Dictionary<IntPtr, string>();
        if (enabled && selected.Count > 0)
            foreach (var w in EnumAppWindows()) {
                string n = NameOf(w.Pid) ?? "";
                if (w.Minimized || !selected.Contains(n)) continue;
                wins.Add(w.H); winNames[w.H] = n;
            }

        foreach (var h in new List<IntPtr>(groups.Keys)) {
            if (wins.Contains(h)) continue;
            groups[h].Dispose(); groups.Remove(h);
            Log("가림 제거 · 남은 창 " + groups.Count);
        }

        IntPtr fg = Win.GetForegroundWindow();
        IntPtr fgRoot = fg == IntPtr.Zero ? IntPtr.Zero : Win.GetAncestor(fg, Win.GA_ROOTOWNER);

        // 영역 조절을 기다리는 중이면, 사용자가 그 프로그램 창을 클릭하는 순간 시작한다.
        if (pendingEdit != null) {
            if (DateTime.Now > pendingUntil) {
                pendingEdit = null;
                Balloon("영역 조절을 취소했습니다", "30초 안에 창을 클릭하지 않았습니다. 다시 하려면 메뉴에서 「보이게 둘 영역 조절하기」 를 누르세요.");
            } else if (fgRoot != IntPtr.Zero) {
                uint pid; Win.GetWindowThreadProcessId(fgRoot, out pid);
                if (pendingEdit.Equals(NameOf(pid), StringComparison.OrdinalIgnoreCase)) {
                    string name = pendingEdit; pendingEdit = null;
                    StartEditor(fgRoot, name);
                }
            }
        }

        Win.POINT cur; Win.GetCursorPos(out cur);
        bool ctrl = (Win.GetAsyncKeyState(Win.VK_CONTROL) & 0x8000) != 0;

        // 주인 창과 딸린 창은 한 묶음으로 다룬다. 쓰는 중이거나 엿보는 중이면 묶음 전체를 걷는다.
        var rects = new Dictionary<IntPtr, Rectangle>();
        var roots = new Dictionary<IntPtr, IntPtr>();
        var peekRoots = new HashSet<IntPtr>();
        foreach (var h in wins) {
            var rect = FrameRect(h);
            IntPtr root = Win.GetAncestor(h, Win.GA_ROOTOWNER);
            if (root == IntPtr.Zero) root = h;
            rects[h] = rect; roots[h] = root;
            if (ctrl && rect.Contains(cur.X, cur.Y)) peekRoots.Add(root);
        }

        foreach (var h in wins) {
            VeilGroup g;
            if (!groups.TryGetValue(h, out g)) {
                g = new VeilGroup(h); groups[h] = g;
                Log("가림 생성 · " + (g.Owned ? "딸린 창" : "주인 창") + " · 가린 창 " + groups.Count);
            }
            string skin = SkinOf(winNames[h]);
            if (g.Skin != skin) { g.Resize(0); g.Skin = skin; }
            var rect = rects[h];
            g.Frame = rect; g.Scale = ScaleOf(h);
            VeilGroup owner = null;
            if (roots[h] != h) groups.TryGetValue(roots[h], out owner);
            g.Owner = owner;
            g.Root = roots[h];

            bool editing = editingTarget != IntPtr.Zero && (h == editingTarget || roots[h] == editingTarget);
            bool active = editing || fg == h || (fgRoot != IntPtr.Zero && roots[h] == fgRoot);
            bool peek = peekRoots.Contains(roots[h]);
            string state = editing ? "걷힘(영역 조절 중)" : active ? "걷힘(사용 중)" : peek ? "걷힘(엿보기)" : "덮임";
            if (state != g.State) { Log((g.Owned ? "딸린 창 " : "주인 창 ") + state); g.State = state; }

            if (active || peek) { g.HideAll(); continue; }

            // 딸린 창은 통째로 덮는다. 주인 창은 보이게 둘 영역을 뺀 나머지를 덮는다.
            var holes = new List<Rectangle>();
            if (!g.Owned) {
                if (g.Kind == null || (now - g.KindAt).TotalSeconds >= 1) { g.Kind = KindOf(h); g.KindAt = now; }
                foreach (var a in areas) {
                    if (!a.Program.Equals(winNames[h], StringComparison.OrdinalIgnoreCase) || !a.Kind.IsSubsetOf(g.Kind)) continue;
                    var r = Rectangle.Intersect(ResolveArea(h, a, rect, g.Scale), rect);
                    if (r.Width >= 8 && r.Height >= 8) holes.Add(r);
                }
            }
            if (holes.Count != g.HoleCount) {
                if (g.HoleCount >= 0 || holes.Count > 0) Log("주인 창 · 보이게 둔 영역 " + holes.Count + "개");
                g.HoleCount = holes.Count;
            }
            var parts = Subtract(rect, holes);
            g.Resize(parts.Count);
            foreach (var v in g.Pieces) if (!v.Visible) { v.Show(); v.Last = Rectangle.Empty; }
            Stack(g, parts);
        }

        // 로그 터미널은 가끔 한 줄씩 올린다.
        foreach (var g in groups.Values) {
            if (g.Owner != null || !Skins.IsLog(g.Skin)) continue;
            if (g.Log == null) g.Log = Skins.Backfill(rng, 40);
            if (DateTime.Now < g.NextLogAt) continue;
            g.Log.Add(Skins.NextLogLine(rng));
            if (g.Log.Count > 200) g.Log.RemoveRange(0, g.Log.Count - 200);
            g.LogSerial++;
            g.NextLogAt = DateTime.Now.AddMilliseconds(rng.Next(500, 2500));
        }

        // 그림이 바뀌었거나 조각이 움직인 묶음만 다시 그린다.
        foreach (var g in groups.Values) {
            if (Skins.IsBlur(g.Skin) || g.Pieces.Count == 0 || !g.Pieces[0].Visible) continue;
            var src = g.Owner ?? g;
            string key = src.Frame + "|" + src.Skin + "|" + src.LogSerial + "|" + string.Join(";", g.Pieces.Select(p => p.Last.ToString()));
            if (key == g.LastPaintKey) continue;
            g.LastPaintKey = key;
            foreach (var p in g.Pieces) p.Invalidate();
        }
    }

    // 창 사각형에서 구멍을 빼고 남은 부분을 겹치지 않는 사각형 조각으로 나눈다.
    // 구멍 하나마다 위·아래는 전체 폭, 왼쪽·오른쪽은 구멍 높이만큼 잘라 낸다.
    static List<Rectangle> Subtract(Rectangle area, List<Rectangle> holes) {
        var parts = new List<Rectangle> { area };
        foreach (var hole in holes) {
            var next = new List<Rectangle>();
            foreach (var p in parts) {
                var x = Rectangle.Intersect(p, hole);
                if (x.Width <= 0 || x.Height <= 0) { next.Add(p); continue; }
                if (x.Top > p.Top) next.Add(new Rectangle(p.Left, p.Top, p.Width, x.Top - p.Top));
                if (x.Bottom < p.Bottom) next.Add(new Rectangle(p.Left, x.Bottom, p.Width, p.Bottom - x.Bottom));
                if (x.Left > p.Left) next.Add(new Rectangle(p.Left, x.Top, x.Left - p.Left, x.Height));
                if (x.Right < p.Right) next.Add(new Rectangle(x.Right, x.Top, p.Right - x.Right, x.Height));
            }
            parts = next;
        }
        return parts;
    }

    // 조각을 대상 창 「바로 위」 에 차례로 쌓는다. 맨 위로 올리면 대상 앞에 있는 다른 창까지 덮는다.
    // 이미 그 순서·그 자리에 있으면 건드리지 않는다.
    static void Stack(VeilGroup g, List<Rectangle> parts) {
        var mine = new HashSet<IntPtr>(g.Pieces.Select(p => p.Handle));
        bool ordered = true;
        IntPtr walk = g.Target;
        for (int i = 0; i < g.Pieces.Count && ordered; i++) {
            // 사이에 끼는 숨은 창(WinForms 가 내부에서 쓰는 창 등)은 화면에 안 보이므로 건너뛴다.
            do { walk = Win.GetWindow(walk, Win.GW_HWNDPREV); }
            while (walk != IntPtr.Zero && !mine.Contains(walk) && !Win.IsWindowVisible(walk));
            ordered = mine.Contains(walk);
        }
        bool moved = false;
        for (int i = 0; i < parts.Count && !moved; i++) moved = parts[i] != g.Pieces[i].Last;
        if (ordered && !moved) return;

        IntPtr above = Win.GetWindow(g.Target, Win.GW_HWNDPREV);
        while (above != IntPtr.Zero && mine.Contains(above)) above = Win.GetWindow(above, Win.GW_HWNDPREV);
        IntPtr after = above; // 첫 조각은 대상 바로 위 창의 아래, 다음 조각은 앞 조각의 아래
        for (int i = 0; i < g.Pieces.Count; i++) {
            var p = g.Pieces[i]; var r = parts[i];
            Win.SetWindowPos(p.Handle, after, r.X, r.Y, r.Width, r.Height, Win.SWP_NOACTIVATE);
            p.Last = r; after = p.Handle;
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
                var r = FrameRect(h);
                if (r.Width < 120 || r.Height < 80) return true;
            }
            list.Add(new WinInfo { H = h, Pid = pid, Minimized = min });
            return true;
        }, IntPtr.Zero);
        return list;
    }

    // 실행 파일 이름(확장자 없이). 그 프로세스 하나에게만 물어본다.
    // Process.GetProcessById 는 부를 때마다 컴퓨터의 모든 프로세스 목록을 읽어서, 창이 많으면 CPU 를 꽤 쓴다.
    // 관리자 권한 프로세스처럼 열 수 없는 것만 예전 방식으로 알아낸다.
    static string NameOf(uint pid) {
        string n;
        if (pidNames.TryGetValue(pid, out n)) return n;
        string path = PathOf(pid);
        if (path != null) n = Path.GetFileNameWithoutExtension(path);
        else {
            try { using (var p = Process.GetProcessById((int)pid)) n = p.ProcessName; }
            catch (ArgumentException) { n = null; }
            catch (InvalidOperationException) { n = null; }
        }
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
    internal static Rectangle FrameRect(IntPtr h) {
        Win.RECT r;
        if (Win.DwmGetWindowAttribute(h, Win.DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(Win.RECT))) != 0)
            Win.GetWindowRect(h, out r);
        return new Rectangle(r.L, r.T, r.R - r.L, r.B - r.T);
    }

    internal static float ScaleOf(IntPtr h) {
        uint dpi = 0;
        try { dpi = Win.GetDpiForWindow(h); } catch (EntryPointNotFoundException) { }
        return dpi == 0 ? 1f : dpi / 96f;
    }

    static void Quit(string why) {
        pollTimer.Stop(); updateTimer.Stop();
        foreach (var h in hooks) Win.UnhookWinEvent(h);
        hooks.Clear();
        if (editor != null && !editor.IsDisposed) editor.Close();
        foreach (var g in groups.Values) g.Dispose();
        groups.Clear();
        tray.Visible = false; tray.Dispose();
        Log("끝 · " + why);
        Application.ExitThread();
    }

    // 창 제목과 화면 내용은 남기지 않는다. 대화방 이름이나 대화 같은 개인정보가 들어 있다.
    static void Log(string msg) {
        try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine, Encoding.UTF8); }
        catch (IOException) { }
    }
}
