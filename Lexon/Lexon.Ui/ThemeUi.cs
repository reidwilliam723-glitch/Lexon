using System.Reflection;
using Lexon.Core.Theming;

namespace Lexon.Ui;

public static class ThemeUi
{
    public static Color Background(Theme theme) => ColorTranslator.FromHtml(theme.Colors.Background);

    public static Color Foreground(Theme theme) => ColorTranslator.FromHtml(theme.Colors.Text);

    public static Color Surface(Theme theme) => ColorTranslator.FromHtml(theme.Colors.Surface);

    public static Color Primary(Theme theme) => ColorTranslator.FromHtml(theme.Colors.Primary);

    public static Color InputBack(Theme theme)
    {
        var background = Background(theme);
        var luminance = (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
        return luminance < 140 ? Surface(theme) : Color.White;
    }

    public static void EnableBufferedPaint(Control control)
    {
        typeof(Control).InvokeMember(
            "DoubleBuffered",
            BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            target: control,
            args: new object[] { true });
    }

    public static void AttachComboDrawing(ComboBox combo)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.DrawItem -= DrawComboItem;
        combo.DrawItem += DrawComboItem;

        // Stop a page-scroll gesture from changing this box's value as the cursor
        // passes over it. Every combo box in Settings is built through this method.
        ComboWheel.Guard(combo);
    }

    public static void ApplyToTree(Control root, Theme theme)
    {
        ApplyToControl(root, theme);
    }

    /// <summary>
    /// Recolours a window without showing the per-control paint cascade. WinForms
    /// cannot batch child paints into one frame, so this does not try: it covers the
    /// window with the new background, applies colours underneath, then drops the
    /// cover once every child has already painted. Hidden windows skip the cover.
    /// </summary>
    public static void ApplyToTreeWithoutFlicker(Form form, Theme theme)
    {
        if (!form.IsHandleCreated || !form.Visible)
        {
            ApplyToTree(form, theme);
            return;
        }

        var cover = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Background(theme),
            TabStop = false
        };
        form.Controls.Add(cover);
        cover.BringToFront();
        cover.Update();

        try
        {
            form.SuspendLayout();
            ApplyToTree(form, theme);
            form.ResumeLayout(false);
        }
        finally
        {
            form.Controls.Remove(cover);
            cover.Dispose();
        }
    }

    public static void StyleComboBox(ComboBox combo, Theme theme)
    {
        combo.BackColor = InputBack(theme);
        combo.ForeColor = Foreground(theme);
    }

    private static void ApplyToControl(Control control, Theme theme)
    {
        switch (control)
        {
            case CheckBox check:
                check.UseVisualStyleBackColor = false;
                check.BackColor = Background(theme);
                check.ForeColor = Foreground(theme);
                return;
            case Button button:
                button.BackColor = Primary(theme);
                button.ForeColor = Color.White;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                return;
            case ComboBox combo:
                StyleComboBox(combo, theme);
                return;
            case TextBox or ListBox or ListView:
                control.BackColor = InputBack(theme);
                control.ForeColor = Foreground(theme);
                return;
            default:
                control.BackColor = Background(theme);
                control.ForeColor = Foreground(theme);
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyToControl(child, theme);
        }
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        var index = e.Index >= 0 ? e.Index : combo.SelectedIndex;
        // Only the open list uses the highlight; a focused closed box keeping
        // SystemColors.Highlight is why the Theme dropdown stayed a dark slab
        // after switching to Light.
        var selected = combo.DroppedDown && (e.State & DrawItemState.Selected) != 0;
        var back = selected ? SystemColors.Highlight : combo.BackColor;
        var fore = selected ? SystemColors.HighlightText : combo.ForeColor;

        var size = e.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        using var buffer = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(buffer))
        {
            using var fill = new SolidBrush(back);
            graphics.FillRectangle(fill, new Rectangle(Point.Empty, size));

            if (index >= 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    combo.GetItemText(combo.Items[index]),
                    combo.Font,
                    new Rectangle(Point.Empty, size),
                    fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        e.Graphics.DrawImageUnscaled(buffer, e.Bounds.Location);
    }
}
