using Lexon.Core.Grammar;
using Lexon.Core.Models;
using Lexon.Core.Theming;
using Lexon.Ui;

namespace Lexon.Settings;

public class TerminologyForm : Form
{
    private readonly ThemeManager? _themeManager;
    private readonly ListBox _globalList = new();
    private readonly TextBox _addGlobalBox = new();
    private readonly ListBox _appList = new();
    private readonly TextBox _appTermsBox = new();

    public IReadOnlyList<string> GlobalTerms { get; private set; } = [];
    public IReadOnlyList<string> AppOverrideRows { get; private set; } = [];

    public TerminologyForm(
        IEnumerable<string>? globalTerms,
        IEnumerable<string>? appOverrideRows,
        ThemeManager? themeManager)
    {
        _themeManager = themeManager;
        InitializeComponent();
        foreach (var term in TerminologyList.ParseGlobal(globalTerms))
        {
            _globalList.Items.Add(term);
        }

        foreach (var pair in TerminologyList.ParseAppRows(appOverrideRows))
        {
            var row = TerminologyList.FormatAppRow(pair.Key, pair.Value);
            if (!string.IsNullOrEmpty(row))
            {
                _appList.Items.Add(row);
            }
        }

        ApplyTheme();
        if (_themeManager != null)
        {
            _themeManager.ThemeChanged += OnExternalThemeChanged;
            FormClosed += (_, _) => _themeManager.ThemeChanged -= OnExternalThemeChanged;
        }
    }

    private void OnExternalThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnExternalThemeChanged, sender, e);
            return;
        }

        ApplyTheme();
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Custom terminology";
        ClientSize = new Size(560, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ThemeUi.EnableBufferedPaint(this);
        Font = new Font("Segoe UI", 9);

        var intro = new Label
        {
            Text = "Names, products, and technical terms Lexon should not autocorrect or flag as typos.",
            Location = new Point(20, 16),
            Size = new Size(520, 36)
        };
        Controls.Add(intro);

        var globalCaption = new Label
        {
            Text = "Global terms",
            Location = new Point(20, 56),
            AutoSize = true
        };
        Controls.Add(globalCaption);

        _globalList.Location = new Point(20, 80);
        _globalList.Size = new Size(380, 160);
        Controls.Add(_globalList);

        var removeGlobal = CreateButton("Remove", 416, 80);
        removeGlobal.Click += (_, _) =>
        {
            if (_globalList.SelectedIndex >= 0)
            {
                _globalList.Items.RemoveAt(_globalList.SelectedIndex);
            }
        };
        Controls.Add(removeGlobal);

        _addGlobalBox.Location = new Point(20, 252);
        _addGlobalBox.Size = new Size(270, 27);
        _addGlobalBox.PlaceholderText = "Add a protected term";
        Controls.Add(_addGlobalBox);

        var addGlobal = CreateButton("Add", 300, 248);
        addGlobal.Click += OnAddGlobal;
        Controls.Add(addGlobal);

        var appCaption = new Label
        {
            Text = "Per-app lists",
            Location = new Point(20, 296),
            AutoSize = true
        };
        Controls.Add(appCaption);

        _appList.Location = new Point(20, 320);
        _appList.Size = new Size(380, 110);
        Controls.Add(_appList);

        var removeApp = CreateButton("Remove", 416, 320);
        removeApp.Click += (_, _) =>
        {
            if (_appList.SelectedIndex >= 0)
            {
                _appList.Items.RemoveAt(_appList.SelectedIndex);
            }
        };
        Controls.Add(removeApp);

        _appTermsBox.Location = new Point(20, 444);
        _appTermsBox.Size = new Size(270, 27);
        _appTermsBox.PlaceholderText = "Terms for app (comma-separated)";
        Controls.Add(_appTermsBox);

        var pickApp = CreateButton("Pick app…", 300, 440);
        pickApp.Click += OnPickApp;
        Controls.Add(pickApp);

        var ok = CreateButton("OK", 300, 500);
        ok.Click += OnOk;
        Controls.Add(ok);

        var cancel = CreateButton("Cancel", 416, 500);
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static Button CreateButton(string text, int x, int y)
    {
        return new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(108, 36)
        };
    }

    private void OnAddGlobal(object? sender, EventArgs e)
    {
        var term = TerminologyList.Normalize(_addGlobalBox.Text);
        if (term.Length == 0)
        {
            return;
        }

        if (term.Contains('|'))
        {
            MessageBox.Show(
                this,
                "A term cannot contain |.",
                "Custom terminology",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        foreach (var existing in _globalList.Items)
        {
            if (existing is string s
                && string.Equals(s, term, StringComparison.OrdinalIgnoreCase))
            {
                _addGlobalBox.Clear();
                return;
            }
        }

        _globalList.Items.Add(term);
        _addGlobalBox.Clear();
    }

    private void OnPickApp(object? sender, EventArgs e)
    {
        var rawTerms = (_appTermsBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (rawTerms.Any(term => term.Contains('|')))
        {
            MessageBox.Show(
                this,
                "A term cannot contain |.",
                "Per-app terminology",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var terms = TerminologyList.ParseGlobal(rawTerms);
        if (terms.Count == 0)
        {
            MessageBox.Show(
                this,
                "Enter one or more terms (comma-separated) before picking an app.",
                "Per-app terminology",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var picker = new ProcessPickerForm("Select a running application for these terms:");
        if (picker.ShowDialog(this) != DialogResult.OK
            || string.IsNullOrWhiteSpace(picker.SelectedProcessName))
        {
            return;
        }

        var combined = new List<string>(terms);
        for (var i = _appList.Items.Count - 1; i >= 0; i--)
        {
            if (_appList.Items[i] is string existing
                && TerminologyList.TryParseAppRow(existing, out var existingApp, out var existingTerms)
                && string.Equals(
                    existingApp,
                    AppCategoryMapper.EnsureExeExtension(picker.SelectedProcessName),
                    StringComparison.OrdinalIgnoreCase))
            {
                combined.InsertRange(0, existingTerms);
                _appList.Items.RemoveAt(i);
            }
        }

        var row = TerminologyList.FormatAppRow(picker.SelectedProcessName, combined);
        if (string.IsNullOrEmpty(row))
        {
            return;
        }

        _appList.Items.Add(row);
        _appTermsBox.Clear();
    }

    private void OnOk(object? sender, EventArgs e)
    {
        GlobalTerms = TerminologyList.ParseGlobal(
            _globalList.Items.Cast<object>().Select(i => i.ToString() ?? string.Empty));
        AppOverrideRows = TerminologyList.ParseAppRows(
                _appList.Items.Cast<object>().Select(i => i.ToString() ?? string.Empty))
            .Select(pair => TerminologyList.FormatAppRow(pair.Key, pair.Value))
            .Where(row => !string.IsNullOrEmpty(row))
            .ToList();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ApplyTheme()
    {
        if (_themeManager == null)
        {
            return;
        }

        ThemeUi.ApplyToTreeWithoutFlicker(this, _themeManager.CurrentTheme);
    }
}
