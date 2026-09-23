namespace Lexon.Settings;

/// <summary>
/// One-time first-run coach shown after onboarding.
/// </summary>
internal sealed class CoachForm : Form
{
    public CoachForm()
    {
        Text = "Try Lexon";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(440, 248);
        Font = new Font("Segoe UI", 9);
        BackColor = Color.White;
        Icon = Lexon.Ui.LexonIconFactory.CreateApplicationIcon();

        var title = new Label
        {
            Text = "A 10-second try-out",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(24, 20),
            AutoSize = true
        };

        var body = new Label
        {
            Text =
                "1. Open Notepad.\n" +
                "2. Type a word — suggestions appear above or below the caret.\n" +
                "3. Press 1, 2, or 3 to accept a prediction.\n\n" +
                "Ctrl+Shift+Z undoes the last Lexon change. Right-click the tray icon to pause.",
            Location = new Point(24, 58),
            Size = new Size(392, 130)
        };

        var gotIt = new Button
        {
            Text = "Got it",
            Size = new Size(100, 32),
            Location = new Point(316, 198),
            DialogResult = DialogResult.OK
        };
        AcceptButton = gotIt;

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(gotIt);
    }
}
