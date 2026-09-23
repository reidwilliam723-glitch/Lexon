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
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 340);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        Icon = Lexon.Ui.LexonIconFactory.CreateApplicationIcon();

        var title = new Label
        {
            Text = "A 10-second try-out",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(32, 28),
            AutoSize = true
        };

        var body = new Label
        {
            Text =
                "1. Open Notepad.\n" +
                "2. Type a word — suggestions appear above or below the caret.\n" +
                "3. Press 1, 2, or 3 to accept a prediction.\n\n" +
                "Ctrl+Shift+Z undoes the last Lexon change. Right-click the tray icon to pause.",
            Location = new Point(32, 78),
            Size = new Size(496, 180)
        };

        var gotIt = new Button
        {
            Text = "Got it",
            Size = new Size(108, 36),
            Location = new Point(420, 278),
            DialogResult = DialogResult.OK
        };
        AcceptButton = gotIt;

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(gotIt);
    }
}
