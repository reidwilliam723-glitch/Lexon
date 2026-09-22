using Lexon.Privacy;
using Lexon.Profiles;
using Lexon.Storage;
using System.Diagnostics;
using System.Text;

namespace Lexon.Settings;

internal static class SettingsPaintProbe
{
    public static bool Enabled { get; private set; }

    public static string LogPath { get; private set; } = Path.Combine(Path.GetTempPath(), "lexon-settings-probe.log");

    private static readonly Stopwatch Clock = new();
    private static readonly object Gate = new();
    private static readonly List<string> Lines = [];

    public static int Paint;
    public static int Erase;
    public static int Size;
    public static int WindowPos;
    public static int SetRedraw;
    public static int FieldSizes;
    public static int FreezeBegin;
    public static int FreezeEnd;
    public static int CoverOn;
    public static int CoverOff;
    public static int Wide;
    public static int Compact;
    public static int LayoutMode;
    public static int Resize;

    public static void Enable()
    {
        Enabled = true;
        LogPath = Path.Combine(Path.GetTempPath(), "lexon-settings-probe.log");
        Clock.Restart();
        lock (Gate)
        {
            Lines.Clear();
        }

        File.WriteAllText(LogPath, $"probe start {DateTime.Now:O}{Environment.NewLine}");
    }

    public static void Mark(string label) => Log("MARK " + label);

    public static void Log(string message)
    {
        if (!Enabled)
        {
            return;
        }

        var line = $"+{Clock.Elapsed.TotalMilliseconds,8:0.0}ms  {message}";
        lock (Gate)
        {
            Lines.Add(line);
        }

        File.AppendAllText(LogPath, line + Environment.NewLine);
    }

    public static void OnWndProc(int msg)
    {
        if (!Enabled)
        {
            return;
        }

        switch (msg)
        {
            case 0x000F:
                Paint++;
                if (Paint <= 30 || Paint % 20 == 0)
                {
                    Log($"WM_PAINT n={Paint}");
                }

                break;
            case 0x0014:
                Erase++;
                if (Erase <= 30 || Erase % 20 == 0)
                {
                    Log($"WM_ERASEBKGND n={Erase}");
                }

                break;
            case 0x0005:
                Size++;
                Log($"WM_SIZE n={Size}");
                break;
            case 0x000B:
                SetRedraw++;
                Log($"WM_SETREDRAW n={SetRedraw}");
                break;
            case 0x0047:
                WindowPos++;
                if (WindowPos <= 20 || WindowPos % 10 == 0)
                {
                    Log($"WM_WINDOWPOSCHANGED n={WindowPos}");
                }

                break;
        }
    }

    public static string Summary()
    {
        var text = new StringBuilder();
        text.AppendLine();
        text.AppendLine("=== SUMMARY ===");
        text.AppendLine($"WM_PAINT (form)     {Paint}");
        text.AppendLine($"WM_ERASEBKGND       {Erase}");
        text.AppendLine($"WM_SIZE             {Size}");
        text.AppendLine($"WM_WINDOWPOSCHANGED {WindowPos}");
        text.AppendLine($"WM_SETREDRAW        {SetRedraw}");
        text.AppendLine($"UpdateLayoutMode    {LayoutMode}");
        text.AppendLine($"Resize handler      {Resize}");
        text.AppendLine($"ApplyFieldSizes     {FieldSizes}");
        text.AppendLine($"ApplyWideLayout     {Wide}");
        text.AppendLine($"ApplyCompactLayout  {Compact}");
        text.AppendLine($"BeginPaintFreeze    {FreezeBegin}");
        text.AppendLine($"EndPaintFreeze      {FreezeEnd}");
        text.AppendLine($"Cover on            {CoverOn}");
        text.AppendLine($"Cover off           {CoverOff}");
        text.AppendLine($"log                 {LogPath}");
        return text.ToString();
    }

    public static void Run()
    {
        Enable();
        ApplicationConfiguration.Initialize();

        SettingsForm? form = null;
        try
        {
            Log("construct SettingsForm (isolated storage)");
            var storeDir = Path.Combine(Path.GetTempPath(), "lexon-settings-probe-store");
            Directory.CreateDirectory(storeDir);
            var storage = new EncryptedStorage(storeDir);
            var profile = new Profile(storage) { Id = Profile.DefaultProfileId };
            form = new SettingsForm(profile, new PrivacyGuard(), storage);
            Log($"constructed handle={form.IsHandleCreated} visible={form.Visible} state={form.WindowState} size={form.Size}");

            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(80, 80);
            form.FormClosing += (_, e) => e.Cancel = false;
            form.FormClosed += (_, _) => Application.ExitThread();

            var step = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (_, _) =>
            {
                if (form == null || form.IsDisposed)
                {
                    timer.Stop();
                    Application.ExitThread();
                    return;
                }

                step++;
                try
                {
                    switch (step)
                    {
                        case 1:
                            Mark("SHOW/Present");
                            form.Present();
                            Log($"after Present visible={form.Visible} state={form.WindowState} size={form.Size}");
                            break;
                        case 3:
                            Mark("MAXIMIZE");
                            SendMessage(form.Handle, 0x0112, (IntPtr)0xF030, IntPtr.Zero);
                            Log($"after maximize state={form.WindowState} size={form.Size} wide={form.LayoutIsWide}");
                            break;
                        case 6:
                            Mark("RESTORE");
                            SendMessage(form.Handle, 0x0112, (IntPtr)0xF120, IntPtr.Zero);
                            Log($"after restore state={form.WindowState} size={form.Size} wide={form.LayoutIsWide}");
                            break;
                        case 8:
                            Mark("REFRESH");
                            form.Refresh();
                            break;
                        case 10:
                            timer.Stop();
                            Mark("CLOSE");
                            form.Close();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log("STEP-ERROR " + ex);
                    timer.Stop();
                    form.Close();
                }
            };
            timer.Start();
            var watchdog = new System.Windows.Forms.Timer { Interval = 15000 };
            watchdog.Tick += (_, _) =>
            {
                watchdog.Stop();
                Log("WATCHDOG timeout");
                form.Close();
                Application.ExitThread();
            };
            watchdog.Start();
            Application.Run();
        }
        finally
        {
            form?.Dispose();
            var summary = Summary();
            File.AppendAllText(LogPath, summary);
            var copy = Path.Combine(AppContext.BaseDirectory, "lexon-settings-probe.log");
            File.Copy(LogPath, copy, overwrite: true);
            var desktopCopy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Lexon",
                "Lexon",
                "lexon-settings-probe.log");
            try
            {
                File.Copy(LogPath, desktopCopy, overwrite: true);
            }
            catch
            {
                // Probe still wrote the temp log.
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
