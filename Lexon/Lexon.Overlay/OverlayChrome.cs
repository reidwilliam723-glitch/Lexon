using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Lexon.Overlay;

/// <summary>
/// Shared drawing helpers for Lexon popups.
/// </summary>
public sealed class OverlayChrome
{
    public const string FontName = "Segoe UI";
    public const int CornerRadius = 10;

    private readonly OverlayThemePalette _palette;

    public OverlayChrome(OverlayThemePalette palette)
    {
        _palette = palette;
    }

    public Color Background => _palette.Background;
    public Color Border => _palette.Border;
    public Color Text => _palette.Text;
    public Color Muted => _palette.Muted;
    public Color Primary => _palette.Primary;
    public Color Add => _palette.Add;
    public Color Remove => _palette.Remove;
    public Color PreviewInset => _palette.PreviewInset;
    public Color ProgressTrack => _palette.ProgressTrack;
    public Color ScrollTrack => _palette.ScrollTrack;
    public Color ScrollThumb => _palette.ScrollThumb;
    public Color GrammarAccent => _palette.GrammarAccent;
    public Color GrammarHighlight => _palette.GrammarHighlight;
    public Color SelectedBackground => _palette.SelectedBackground;
    public Color SelectedText => _palette.SelectedText;
    public Color HoverText => _palette.Text;

    public void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Background);
    }

    public void DrawBorder(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(Border);
        g.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    public void DrawButton(Graphics g, Rectangle rect, string text, bool primary)
    {
        using var path = Rounded(rect, 4);
        using var fill = new SolidBrush(primary ? Primary : _palette.SecondaryButton);
        using var textBrush = new SolidBrush(Color.White);
        using var font = new Font(FontName, 9);
        g.FillPath(fill, path);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, textBrush, rect.X + (rect.Width - size.Width) / 2, rect.Y + (rect.Height - size.Height) / 2);
    }

    public static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
