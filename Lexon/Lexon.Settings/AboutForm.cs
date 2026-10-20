using System.Reflection;
using System.Windows.Forms;
using Lexon.Core;
using Lexon.SettingsModel;

namespace Lexon.Settings;

/// <summary>
/// About dialog for Lexon (tray menu).
/// </summary>
public class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About Lexon";
        Size = new Size(560, 640);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Icon = Lexon.Ui.LexonIconFactory.CreateApplicationIcon();
        InitializeComponents();
    }

    public static void ShowPrivacyPolicy(IWin32Window? owner = null)
        => ShowLegalDocument(owner, "Privacy Policy", "PRIVACY_POLICY.md");

    public static void ShowTermsOfService(IWin32Window? owner = null)
        => ShowLegalDocument(owner, "Terms of Service", "TERMS_OF_SERVICE.md");

    public static void ShowLicense(IWin32Window? owner = null)
        => ShowLegalDocument(owner, "License Agreement", "LICENSE");

    private void InitializeComponents()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 20, 24, 16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Width = 490
        };

        stack.Controls.Add(MakeLabel("Lexon", new Font("Segoe UI", 22, FontStyle.Bold), AutoSize: true));
        stack.Controls.Add(MakeLabel($"Version {AppVersion.Current}", new Font("Segoe UI", 10),
            foreColor: Color.FromArgb(90, 90, 90), margin: new Padding(0, 4, 0, 8)));
        stack.Controls.Add(MakeLabel(AboutCopy.Tagline, new Font("Segoe UI", 9),
            width: 480, height: 40, foreColor: Color.FromArgb(70, 70, 70)));
        stack.Controls.Add(MakeLabel("Copyright © 2026 Lexon. All rights reserved.", new Font("Segoe UI", 8),
            foreColor: Color.FromArgb(110, 110, 110), margin: new Padding(0, 0, 0, 16)));

        stack.Controls.Add(MakeLabel(AboutCopy.NoteTitle, new Font("Segoe UI", 11, FontStyle.Bold),
            margin: new Padding(0, 0, 0, 8)));
        stack.Controls.Add(MakeWrappedLabel(AboutCopy.NoteBody, new Font("Segoe UI", 9), 480,
            margin: new Padding(0, 0, 0, 16)));

        stack.Controls.Add(MakeLink("Privacy Policy", (_, _) => ShowPrivacyPolicy(this)));
        stack.Controls.Add(MakeLink("Terms of Service", (_, _) => ShowTermsOfService(this)));
        stack.Controls.Add(MakeLink("License Agreement", (_, _) => ShowLicense(this)));
        stack.Controls.Add(MakeLabel(AboutCopy.ContactEmail, new Font("Segoe UI", 9),
            margin: new Padding(0, 12, 0, 0)));

        scroll.Controls.Add(stack);
        root.Controls.Add(scroll, 0, 0);

        var ok = new Button
        {
            Text = "OK",
            Size = new Size(100, 30),
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right
        };
        ok.Click += (_, _) => Close();
        var footer = new Panel { Height = 40, Dock = DockStyle.Fill };
        footer.Controls.Add(ok);
        footer.Resize += (_, _) => ok.Location = new Point(footer.Width - ok.Width, 5);
        root.Controls.Add(footer, 0, 1);

        Controls.Add(root);
        AcceptButton = ok;
    }

    private static Label MakeLabel(
        string text,
        Font font,
        int? width = null,
        int? height = null,
        Color? foreColor = null,
        Padding? margin = null,
        bool AutoSize = false)
    {
        var label = new Label
        {
            Text = text,
            Font = font,
            AutoSize = AutoSize,
            Margin = margin ?? Padding.Empty
        };
        if (foreColor is Color color)
        {
            label.ForeColor = color;
        }

        if (width is int w)
        {
            label.Width = w;
        }

        if (height is int h)
        {
            label.Height = h;
        }

        return label;
    }

    private static Label MakeWrappedLabel(string text, Font font, int width, Padding? margin = null)
    {
        return new Label
        {
            Text = text,
            Font = font,
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Margin = margin ?? Padding.Empty
        };
    }

    private static LinkLabel MakeLink(string text, LinkLabelLinkClickedEventHandler handler)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            LinkColor = Color.Blue,
            Margin = new Padding(0, 0, 0, 6)
        };
        link.LinkClicked += handler;
        return link;
    }

    private static void ShowLegalDocument(IWin32Window? owner, string title, string resourceName)
    {
        string content;
        try
        {
            content = GetEmbeddedResource(resourceName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                $"Failed to load {title}: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        using var legalForm = new Form
        {
            Text = title,
            Size = new Size(700, 500),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = new Size(600, 400)
        };

        var textBox = new TextBox
        {
            Text = content,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.White
        };

        var closeButton = new Button
        {
            Text = "Close",
            Size = new Size(100, 30),
            DialogResult = DialogResult.OK
        };

        var panel = new Panel
        {
            Height = 50,
            Dock = DockStyle.Bottom
        };

        panel.Resize += (_, _) => closeButton.Location = new Point(panel.Width - 110, 10);
        panel.Controls.Add(closeButton);
        legalForm.Controls.Add(textBox);
        legalForm.Controls.Add(panel);
        legalForm.AcceptButton = closeButton;

        if (owner != null)
        {
            legalForm.ShowDialog(owner);
        }
        else
        {
            legalForm.ShowDialog();
        }
    }

    private static string GetEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var fullResourceName = resourceNames.FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(fullResourceName))
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Failed to load embedded resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
