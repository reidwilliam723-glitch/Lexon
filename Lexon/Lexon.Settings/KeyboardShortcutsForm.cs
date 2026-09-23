using Lexon.Input;
using System.Drawing;

namespace Lexon.Settings;

/// <summary>
/// Form displaying keyboard shortcuts reference
/// </summary>
public partial class KeyboardShortcutsForm : Form
{
    private readonly KeyboardShortcutManager _shortcutManager;

    public KeyboardShortcutsForm(KeyboardShortcutManager shortcutManager)
    {
        _shortcutManager = shortcutManager ?? throw new ArgumentNullException(nameof(shortcutManager));
        InitializeComponent();
        LoadShortcuts();
    }

    private void InitializeComponent()
    {
        this.Text = "Lexon - Keyboard Shortcuts";
        this.ClientSize = new Size(720, 620);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.Icon = Lexon.Ui.LexonIconFactory.CreateApplicationIcon();

        var titleLabel = new Label
        {
            Text = "Keyboard Shortcuts",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(24, 20),
            AutoSize = true
        };
        this.Controls.Add(titleLabel);

        var shortcutsPanel = new Panel
        {
            Location = new Point(24, 64),
            Size = new Size(672, 490),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };
        this.Controls.Add(shortcutsPanel);

        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(616, 570),
            Size = new Size(80, 32)
        };
        closeButton.Click += (s, e) => this.Close();
        this.Controls.Add(closeButton);

        _shortcutsPanel = shortcutsPanel;
    }

    private Panel _shortcutsPanel = null!;

    private void LoadShortcuts()
    {
        _shortcutsPanel.Controls.Clear();

        var shortcuts = new Dictionary<string, string>
        {
            { "AcceptSuggestion", "Accept the current suggestion" },
            { "DismissSuggestion", "Dismiss the suggestion popup" },
            { "NavigateUp", "Navigate up in the suggestion list" },
            { "NavigateDown", "Navigate down in the suggestion list" },
            { "QuickToggle", "Double-press Ctrl to pause Lexon for the duration set in Settings" },
            { "OpenSettings", "Open Settings (Ctrl+Shift+S)" },
            { "UndoLexon", "Undo last Lexon change (also on the tray menu)" }
        };

        int y = 10;
        foreach (var (name, description) in shortcuts)
        {
            var shortcut = _shortcutManager.GetShortcut(name);
            if (shortcut != null)
            {
                var keyLabel = new Label
                {
                    Text = _shortcutManager.GetShortcutDisplayText(shortcut),
                    Font = new Font("Consolas", 11, FontStyle.Bold),
                    Location = new Point(16, y),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(0, 120, 215)
                };

                var descLabel = new Label
                {
                    Text = description,
                    Font = new Font("Segoe UI", 10),
                    Location = new Point(240, y),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(80, 80, 80)
                };

                _shortcutsPanel.Controls.Add(keyLabel);
                _shortcutsPanel.Controls.Add(descLabel);
                y += 40;
            }
        }

        // Add note about writing assistance shortcuts - loaded live from KeyboardShortcutManager
        var noteLabel = new Label
        {
            Text = "Writing assistance. Rewrite and grammar work without these shortcuts:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(16, y + 10),
            AutoSize = true
        };
        _shortcutsPanel.Controls.Add(noteLabel);
        y += 25;

        var writingShortcuts = new[]
        {
            ("Rewrite", "Optional — rewrite selected text (also: select text, then click Aa)"),
            ("ImproveGrammar", "Optional — check grammar now (also runs after you pause typing)"),
            ("FormalTone", "Change to formal tone"),
            ("CasualTone", "Change to casual tone"),
            ("ProfessionalTone", "Change to professional tone")
        };

        foreach (var (name, desc) in writingShortcuts)
        {
            var shortcut = _shortcutManager.GetShortcut(name);
            if (shortcut != null)
            {
                var keyLabel = new Label
                {
                    Text = _shortcutManager.GetShortcutDisplayText(shortcut),
                    Font = new Font("Consolas", 11, FontStyle.Bold),
                    Location = new Point(16, y),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(0, 120, 215)
                };

                var descLabel = new Label
                {
                    Text = desc,
                    Font = new Font("Segoe UI", 10),
                    Location = new Point(240, y),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(80, 80, 80)
                };

                _shortcutsPanel.Controls.Add(keyLabel);
                _shortcutsPanel.Controls.Add(descLabel);
                y += 36;
            }
        }
    }
}
