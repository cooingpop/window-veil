using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class Program {
    [STAThread]
    private static void Main() {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowVeil");
        string logPath = Path.Combine(dir, "veil.log");
        try {
            Directory.CreateDirectory(dir);
            VeilApp.Run(0, logPath);
        } catch (Exception ex) {
            try {
                File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  failed to start: " + ex + Environment.NewLine, Encoding.UTF8);
            } catch (IOException) { }
            MessageBox.Show("Window Veil could not start. See %APPDATA%\\WindowVeil\\veil.log for details.", "Window Veil", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
