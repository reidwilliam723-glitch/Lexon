using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Lexon.Core;
using Lexon.Ui;

namespace Lexon.Service;

/// <summary>
/// Manages the system tray icon with dynamic status indicators
/// </summary>
public class SystemTrayManager : IDisposable
{
    private const int TrayTextMax = 63;

    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _contextMenu = null!;
    private ToolStripMenuItem _statusMenuItem = null!;
    private ToolStripMenuItem _toggleMenuItem = null!;
    private ToolStripMenuItem _pauseFifteenItem = null!;
    private ToolStripMenuItem _resumeItem = null!;
    private ToolStripMenuItem _pauseAppItem = null!;
    private ToolStripMenuItem _undoItem = null!;
    private ServiceStatus _currentStatus = ServiceStatus.Inactive;
    private string _lastTooltip = string.Empty;
    private bool _paused;
    private StatusToastForm? _statusToast;
    private System.Windows.Forms.Timer? _statusToastTimer;
    private readonly Dictionary<ServiceStatus, Icon> _icons = new();
    private TrayMessageWindow? _taskbarWatcher;

    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? ToggleRequested;
    public event EventHandler? KeyboardShortcutsRequested;
    public event EventHandler? UpdatesRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? UndoRequested;
    public event EventHandler? PauseFifteenRequested;
    public event EventHandler? PauseThisAppRequested;
    public event EventHandler? ResumeRequested;

    public SystemTrayManager()
    {
        InitializeTrayIcon();
        _taskbarWatcher = new TrayMessageWindow(RestoreAfterExplorerRestart);
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = $"Lexon {AppVersion.Current} - Inactive",
            Visible = true
        };

        _notifyIcon.Icon = GetCachedIcon(ServiceStatus.Inactive);

        _contextMenu = new ContextMenuStrip();

        _statusMenuItem = new ToolStripMenuItem("Status: Inactive") { Enabled = false };
        _toggleMenuItem = new ToolStripMenuItem("Enable Lexon");
        _toggleMenuItem.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);

        _pauseFifteenItem = new ToolStripMenuItem("Pause for 15 minutes");
        _pauseFifteenItem.Click += (_, _) => PauseFifteenRequested?.Invoke(this, EventArgs.Empty);
        _resumeItem = new ToolStripMenuItem("Resume now");
        _resumeItem.Visible = false;
        _resumeItem.Click += (_, _) => ResumeRequested?.Invoke(this, EventArgs.Empty);
        _pauseAppItem = new ToolStripMenuItem("Pause in this app");
        _pauseAppItem.Click += (_, _) => PauseThisAppRequested?.Invoke(this, EventArgs.Empty);

        _undoItem = new ToolStripMenuItem("Undo last change\tCtrl+Shift+Z") { Enabled = false };
        _undoItem.Click += (_, _) => UndoRequested?.Invoke(this, EventArgs.Empty);

        var settingsMenuItem = new ToolStripMenuItem("Settings");
        settingsMenuItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var shortcutsMenuItem = new ToolStripMenuItem("Keyboard Shortcuts");
        shortcutsMenuItem.Click += (_, _) => KeyboardShortcutsRequested?.Invoke(this, EventArgs.Empty);
        var aboutMenuItem = new ToolStripMenuItem("About Lexon");
        aboutMenuItem.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        var updatesMenuItem = new ToolStripMenuItem("Check for updates…");
        updatesMenuItem.Click += (_, _) => UpdatesRequested?.Invoke(this, EventArgs.Empty);
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(_statusMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_toggleMenuItem);
        _contextMenu.Items.Add(_pauseFifteenItem);
        _contextMenu.Items.Add(_resumeItem);
        _contextMenu.Items.Add(_pauseAppItem);
        _contextMenu.Items.Add(_undoItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(settingsMenuItem);
        _contextMenu.Items.Add(shortcutsMenuItem);
        _contextMenu.Items.Add(aboutMenuItem);
        _contextMenu.Items.Add(updatesMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _ = _contextMenu.Handle;
    }

    public void SetStatus(ServiceStatus status, string? additionalInfo = null)
    {
        InvokeOnUiThread(() => SetStatusCore(status, additionalInfo));
    }

    public void SetPaused(bool paused, string? reason = null)
    {
        InvokeOnUiThread(() =>
        {
            _paused = paused;
            _pauseFifteenItem.Visible = !paused;
            _resumeItem.Visible = paused;
            _toggleMenuItem.Text = paused || statusIsInactive()
                ? "Enable Lexon"
                : "Disable Lexon";

            if (paused)
            {
                SetStatusCore(ServiceStatus.Inactive, reason ?? "Paused");
            }

            bool statusIsInactive() => _currentStatus == ServiceStatus.Inactive && !paused;
        });
    }

    public void SetUndoAvailability(bool canUndo, string? label)
    {
        InvokeOnUiThread(() =>
        {
            _undoItem.Enabled = canUndo;
            _undoItem.Text = string.IsNullOrEmpty(label)
                ? "Undo last change\tCtrl+Shift+Z"
                : $"{label}\tCtrl+Shift+Z";
        });
    }

    public void RestoreAfterExplorerRestart()
    {
        InvokeOnUiThread(() =>
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Visible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tray restore failed: {ex.Message}");
            }
        });
    }

    private void SetStatusCore(ServiceStatus status, string? additionalInfo)
    {
        try
        {
            var tooltipText = BuildTooltip(status, additionalInfo);
            var statusChanged = _currentStatus != status;
            _currentStatus = status;

            if (statusChanged || _notifyIcon.Icon == null)
            {
                _notifyIcon.Icon = GetCachedIcon(status);
                if (!_paused)
                {
                    _toggleMenuItem.Text = status == ServiceStatus.Active ? "Disable Lexon" : "Enable Lexon";
                }
            }

            if (tooltipText == _lastTooltip)
            {
                return;
            }

            _lastTooltip = tooltipText;
            SetTrayText(_notifyIcon, tooltipText);
            _statusMenuItem.Text = string.IsNullOrEmpty(additionalInfo)
                ? $"Status: {StatusLabel(status)}"
                : $"Status: {StatusLabel(status)} - {additionalInfo}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray status update failed: {ex.Message}");
        }
    }

    public void InvokeOnUiThread(Action action)
    {
        if (action == null || _contextMenu == null || _contextMenu.IsDisposed)
        {
            return;
        }

        if (!_contextMenu.IsHandleCreated)
        {
            _ = _contextMenu.Handle;
        }

        if (_contextMenu.InvokeRequired)
        {
            _contextMenu.BeginInvoke(action);
            return;
        }

        action();
    }

    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        InvokeOnUiThread(() =>
        {
            try
            {
                _notifyIcon.ShowBalloonTip(3000, title, text, icon);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tray balloon failed: {ex.Message}");
            }
        });
    }

    public void ShowStatusToast(string message, bool enabled)
    {
        InvokeOnUiThread(() => ShowStatusToastCore(message, enabled));
    }

    private void ShowStatusToastCore(string message, bool enabled)
    {
        _statusToastTimer?.Stop();
        _statusToast ??= new StatusToastForm();
        if (_statusToast.IsDisposed)
        {
            _statusToast = new StatusToastForm();
        }

        _statusToast.ShowMessage(message, enabled);

        _statusToastTimer ??= new System.Windows.Forms.Timer { Interval = 2200 };
        _statusToastTimer.Tick -= HideStatusToast;
        _statusToastTimer.Tick += HideStatusToast;
        _statusToastTimer.Start();
    }

    private void HideStatusToast(object? sender, EventArgs e)
    {
        _statusToastTimer?.Stop();
        if (_statusToast != null && !_statusToast.IsDisposed)
        {
            _statusToast.Hide();
        }
    }

    private Icon GetCachedIcon(ServiceStatus status)
    {
        if (_icons.TryGetValue(status, out var icon))
        {
            return icon;
        }

        var fill = status switch
        {
            ServiceStatus.Active => LexonIconFactory.Brand,
            ServiceStatus.Inactive => LexonIconFactory.BrandMuted,
            ServiceStatus.Error => LexonIconFactory.BrandError,
            ServiceStatus.Learning => LexonIconFactory.BrandLearning,
            _ => LexonIconFactory.BrandMuted
        };

        icon = LexonIconFactory.CreateStatusIcon(fill);
        _icons[status] = icon;
        return icon;
    }

    private static string BuildTooltip(ServiceStatus status, string? additionalInfo)
    {
        var tooltipText = $"Lexon {AppVersion.Current} - {StatusLabel(status)}";
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            tooltipText += $" ({additionalInfo})";
        }

        return tooltipText;
    }

    private static string StatusLabel(ServiceStatus status) => status switch
    {
        ServiceStatus.Active => "Active",
        ServiceStatus.Inactive => "Inactive",
        ServiceStatus.Error => "Error",
        ServiceStatus.Learning => "Learning",
        _ => "Unknown"
    };

    private static void SetTrayText(NotifyIcon notifyIcon, string text)
    {
        if (text.Length > TrayTextMax)
        {
            text = text[..(TrayTextMax - 1)] + "…";
        }

        notifyIcon.Text = text;
    }

    public void Dispose()
    {
        _taskbarWatcher?.Dispose();
        _statusToastTimer?.Stop();
        _statusToastTimer?.Dispose();
        _statusToast?.Dispose();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Icon = null;
            _notifyIcon.Dispose();
        }

        _contextMenu?.Dispose();
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
    }

    private sealed class TrayMessageWindow : NativeWindow, IDisposable
    {
        private readonly Action _onTaskbarCreated;
        private readonly uint _taskbarCreated;

        public TrayMessageWindow(Action onTaskbarCreated)
        {
            _onTaskbarCreated = onTaskbarCreated;
            _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
            CreateHandle(new CreateParams
            {
                Caption = "LexonTraySink",
                Parent = new IntPtr(-3)
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)_taskbarCreated)
            {
                _onTaskbarCreated();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);
    }
}

public enum ServiceStatus
{
    Inactive,
    Active,
    Learning,
    Error
}
