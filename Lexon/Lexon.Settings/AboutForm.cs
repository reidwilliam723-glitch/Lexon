using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Lexon.Core;

namespace Lexon.Settings;

/// <summary>
/// About dialog for Lexon application
/// </summary>
public class AboutForm : Form
{
    private Label _titleLabel;
    private Label _versionLabel;
    private Label _copyrightLabel;
    private Label _descriptionLabel;
    private LinkLabel _privacyPolicyLink;
    private LinkLabel _termsOfServiceLink;
    private LinkLabel _licenseLink;
    private LinkLabel _websiteLink;
    private Button _okButton;

    public AboutForm()
    {
        Text = "About Lexon";
        Size = new Size(500, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Icon = Lexon.Ui.LexonIconFactory.CreateApplicationIcon();

        InitializeComponents();
        LoadAssemblyInfo();
    }

    private void InitializeComponents()
    {
        // Title label
        _titleLabel = new Label
        {
            Text = "Lexon",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            Location = new Point(50, 30),
            AutoSize = true
        };

        // Version label
        _versionLabel = new Label
        {
            Text = $"Version {AppVersion.Current}",
            Font = new Font("Segoe UI", 10),
            Location = new Point(50, 70),
            AutoSize = true
        };

        // Description label
        _descriptionLabel = new Label
        {
            Text = "AI-powered typing assistant for Windows with privacy-first design",
            Font = new Font("Segoe UI", 9),
            Location = new Point(50, 100),
            Size = new Size(400, 60),
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        // Copyright label
        _copyrightLabel = new Label
        {
            Text = "Copyright © 2026 Lexon. All rights reserved.",
            Font = new Font("Segoe UI", 8),
            Location = new Point(50, 170),
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100)
        };

        // Privacy Policy link
        _privacyPolicyLink = new LinkLabel
        {
            Text = "Privacy Policy",
            Location = new Point(50, 210),
            AutoSize = true,
            LinkColor = Color.Blue
        };
        _privacyPolicyLink.LinkClicked += OnPrivacyPolicyClicked;

        // Terms of Service link
        _termsOfServiceLink = new LinkLabel
        {
            Text = "Terms of Service",
            Location = new Point(50, 240),
            AutoSize = true,
            LinkColor = Color.Blue
        };
        _termsOfServiceLink.LinkClicked += OnTermsOfServiceClicked;

        // License link
        _licenseLink = new LinkLabel
        {
            Text = "License Agreement",
            Location = new Point(50, 270),
            AutoSize = true,
            LinkColor = Color.Blue
        };
        _licenseLink.LinkClicked += OnLicenseClicked;

        // Website link
        _websiteLink = new LinkLabel
        {
            Text = "https://lexon.com",
            Location = new Point(50, 300),
            AutoSize = true,
            LinkColor = Color.Blue,
            Font = new Font("Segoe UI", 9, FontStyle.Underline)
        };
        _websiteLink.LinkClicked += OnWebsiteClicked;

        // OK button
        _okButton = new Button
        {
            Text = "OK",
            Size = new Size(100, 30),
            Location = new Point(200, 330),
            DialogResult = DialogResult.OK
        };
        _okButton.Click += (s, e) => Close();

        // Add controls to form
        Controls.Add(_titleLabel);
        Controls.Add(_versionLabel);
        Controls.Add(_descriptionLabel);
        Controls.Add(_copyrightLabel);
        Controls.Add(_privacyPolicyLink);
        Controls.Add(_termsOfServiceLink);
        Controls.Add(_licenseLink);
        Controls.Add(_websiteLink);
        Controls.Add(_okButton);

        // Set accept button
        AcceptButton = _okButton;
    }

    private void LoadAssemblyInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Not assembly.GetName().Version: Directory.Build.props pins that to
            // 2.0.0.0, which would report a version the release never shipped.
            _versionLabel.Text = $"Version {AppVersion.Current}";

            var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            if (copyrightAttribute != null)
            {
                _copyrightLabel.Text = copyrightAttribute.Copyright;
            }

            var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
            if (descriptionAttribute != null)
            {
                _descriptionLabel.Text = descriptionAttribute.Description;
            }
        }
        catch
        {
            // Use default values if assembly info loading fails
        }
    }

    private void OnPrivacyPolicyClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            var privacyPolicyText = GetEmbeddedResource("PRIVACY_POLICY.md");
            ShowLegalDocument("Privacy Policy", privacyPolicyText);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load Privacy Policy: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnTermsOfServiceClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            var termsText = GetEmbeddedResource("TERMS_OF_SERVICE.md");
            ShowLegalDocument("Terms of Service", termsText);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load Terms of Service: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnLicenseClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            var licenseText = GetEmbeddedResource("LICENSE");
            ShowLegalDocument("License Agreement", licenseText);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load License: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnWebsiteClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://lexon.com",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open website: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string GetEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var fullResourceName = resourceNames.FirstOrDefault(name => name.EndsWith(resourceName));

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

    private void ShowLegalDocument(string title, string content)
    {
        var legalForm = new Form
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

        closeButton.Location = new Point(panel.Width - 110, 10);
        panel.Controls.Add(closeButton);

        legalForm.Controls.Add(textBox);
        legalForm.Controls.Add(panel);

        legalForm.ShowDialog(this);
    }
}