param(
    [string]$OutFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\WindowVeil.ico'),
    [string]$PreviewFile = ''
)
# Draws the Window Veil icon (a window whose content is mostly covered by a veil, with two lines left visible)
# and writes a multi-size .ico. Small sizes are snapped to whole pixels so they stay sharp.
# Keep this file ASCII only: Windows PowerShell 5.1 reads BOM-less scripts in the system code page.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class VeilIcon {
    static readonly Color BgTop = Color.FromArgb(59, 130, 246);
    static readonly Color BgBottom = Color.FromArgb(30, 64, 175);
    static readonly Color Veil = Color.FromArgb(191, 215, 255);
    static readonly Color Ink = Color.FromArgb(30, 58, 138);
    static readonly Color Dot = Color.FromArgb(147, 180, 245);

    // Coordinates are on a 256 grid and scaled to n. Up to 48 px they are rounded to whole pixels.
    static RectangleF R(int n, float x, float y, float w, float h) {
        float s = n / 256f;
        if (n > 48) return new RectangleF(x * s, y * s, w * s, h * s);
        // Math.Round rounds .5 to even, which makes equal bars come out with different thickness.
        float l = Snap(x * s), t = Snap(y * s), r = Snap((x + w) * s), b = Snap((y + h) * s);
        return new RectangleF(l, t, Math.Max(1, r - l), Math.Max(1, b - t));
    }

    static float Snap(float v) { return (float)Math.Floor(v + 0.5f); }

    static GraphicsPath Rounded(RectangleF r, float radius) {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Bitmap Draw(int n) {
        float s = n / 256f;
        var bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = n > 24 ? SmoothingMode.AntiAlias : SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(Color.Transparent);

            var bg = n > 48 ? R(n, 8, 8, 240, 240) : new RectangleF(0, 0, n, n);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(bg, (n > 48 ? 52 : 40) * s))
            using (var brush = new LinearGradientBrush(new PointF(0, bg.Top), new PointF(0, bg.Bottom + 1), BgTop, BgBottom))
                g.FillPath(brush, path);
            g.SmoothingMode = n > 24 ? SmoothingMode.AntiAlias : SmoothingMode.None;

            // The window
            var win = R(n, 44, 48, 168, 160);
            using (var path = Rounded(win, n > 24 ? 14 * s : 0))
                g.FillPath(Brushes.White, path);

            // Title bar dots, only where they can be seen
            if (n >= 48)
                using (var dot = new SolidBrush(Dot))
                    for (int i = 0; i < 3; i++) g.FillEllipse(dot, R(n, 60 + i * 18, 62, 11, 11));

            // Up to 32 px the bars are laid out in whole pixels from the window edges, so the two lines
            // never merge and keep the same thickness.
            if (n <= 32) {
                float t = Math.Max(1, n / 16), pad = Math.Max(1, Snap(n / 16f));
                float left = win.Left + pad, width = win.Width - pad * 2;
                float y2 = win.Bottom - pad - t, y1 = y2 - t * 2;
                float vTop = win.Top + Math.Max(2, Snap(n * 40 / 256f)), vBottom = y1 - t;
                using (var vb = new SolidBrush(Veil)) g.FillRectangle(vb, left, vTop, width, vBottom - vTop);
                using (var ink = new SolidBrush(Ink)) {
                    g.FillRectangle(ink, left, y1, Snap(width * 0.8f), t);
                    g.FillRectangle(ink, left, y2, Snap(width * 0.5f), t);
                }
                return bmp;
            }

            // The veil over most of the content area, with a soft wave at its lower edge on large sizes
            var veil = R(n, 56, 88, 144, 64);
            using (var vb = new SolidBrush(Veil)) {
                if (n >= 64) {
                    using (var p = new GraphicsPath()) {
                        p.AddLine(veil.Left, veil.Bottom, veil.Left, veil.Top);
                        p.AddLine(veil.Left, veil.Top, veil.Right, veil.Top);
                        p.AddLine(veil.Right, veil.Top, veil.Right, veil.Bottom);
                        float w = veil.Width / 3f, a = 7 * s;
                        for (int i = 0; i < 3; i++) {
                            float x1 = veil.Right - w * i, x0 = x1 - w;
                            p.AddBezier(x1, veil.Bottom, x1 - w * 0.25f, veil.Bottom + a, x0 + w * 0.25f, veil.Bottom + a, x0, veil.Bottom);
                        }
                        p.CloseFigure();
                        g.FillPath(vb, p);
                    }
                } else g.FillRectangle(vb, veil);
            }

            // Two lines of content left visible below the veil
            using (var ink = new SolidBrush(Ink)) {
                g.FillRectangle(ink, R(n, 56, 164, 120, 12));
                g.FillRectangle(ink, R(n, 56, 184, 76, 12));
            }
        }
        return bmp;
    }

    static byte[] Dib(Bitmap bmp) {
        int n = bmp.Width;
        var data = bmp.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, px, (n - 1 - y) * n * 4, n * 4);
        bmp.UnlockBits(data);
        int maskRow = ((n + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms)) {
            w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(px.Length + maskRow * n); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            w.Write(px);
            w.Write(new byte[maskRow * n]);
            return ms.ToArray();
        }
    }

    public static void SaveIco(string path, int[] sizes) {
        var images = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
            using (var bmp = Draw(sizes[i])) {
                if (sizes[i] >= 256) using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); images[i] = ms.ToArray(); }
                else images[i] = Dib(bmp);
            }
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs)) {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++) {
                byte d = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(d); w.Write(d); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32); w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
    }

    // Every size side by side, each also enlarged 4x without smoothing, to check the pixels.
    public static void SavePreview(string path, int[] sizes) {
        int width = 16, height = 0;
        foreach (int n in sizes) { width += n * 5 + 16; height = Math.Max(height, n * 4); }
        using (var sheet = new Bitmap(width, height + 32))
        using (var g = Graphics.FromImage(sheet)) {
            g.Clear(Color.FromArgb(243, 243, 243));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            int x = 16;
            foreach (int n in sizes)
                using (var bmp = Draw(n)) {
                    g.DrawImage(bmp, x, 16, n, n);
                    g.DrawImage(bmp, x + n + 8, 16, n * 4, n * 4);
                    x += n * 5 + 16;
                }
            sheet.Save(path, ImageFormat.Png);
        }
    }
}
'@
$sizes = [int[]](16, 20, 24, 32, 40, 48, 64, 256)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutFile) | Out-Null
[VeilIcon]::SaveIco($OutFile, $sizes)
Write-Output $OutFile
if ($PreviewFile) { [VeilIcon]::SavePreview($PreviewFile, [int[]](16, 20, 24, 32, 48, 64)); Write-Output $PreviewFile }
