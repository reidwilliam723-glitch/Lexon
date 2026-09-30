using System.Drawing;
using Lexon.Core.Theming;

namespace Lexon.Overlay;

/// <summary>
/// Maps <see cref="Theme"/> colors to overlay drawing colors.
/// </summary>
public sealed class OverlayThemePalette
{
    public Color Background { get; private set; }
    public Color Border { get; private set; }
    public Color Text { get; private set; }
    public Color Muted { get; private set; }
    public Color Primary { get; private set; }
    public Color SelectedBackground { get; private set; }
    public Color SelectedText { get; private set; }
    public Color HoverBackground { get; private set; }
    public Color Add { get; private set; }
    public Color Remove { get; private set; }
    public Color GrammarAccent { get; private set; }
    public Color GrammarHighlight { get; private set; }
    public Color ScrollTrack { get; private set; }
    public Color ScrollThumb { get; private set; }
    public Color SecondaryButton { get; private set; }
    public Color PreviewInset { get; private set; }
    public Color ProgressTrack { get; private set; }

    public IReadOnlyDictionary<string, Color> SourceColors { get; private set; }
        = new Dictionary<string, Color>();

    public event EventHandler? Changed;

    public void Apply(Lexon.Core.Theming.Theme theme)
    {
        Background = FromHtml(theme.Colors.Surface);
        Border = FromHtml(theme.Colors.Border);
        Text = FromHtml(theme.Colors.Text);
        Muted = FromHtml(theme.Colors.TextSecondary);
        Primary = FromHtml(theme.Colors.Primary);
        SelectedBackground = FromHtml(theme.Colors.Primary);
        SelectedText = FromHtml(string.IsNullOrWhiteSpace(theme.Colors.OnPrimary) ? "#FFFFFF" : theme.Colors.OnPrimary);
        HoverBackground = Blend(Background, FromHtml(theme.Colors.Background), 0.35f);
        Add = FromHtml(theme.Colors.Success);
        Remove = FromHtml(theme.Colors.Error);
        GrammarAccent = FromHtml(theme.Colors.Warning);
        GrammarHighlight = Blend(FromHtml(theme.Colors.Warning), Background, 0.45f);
        ScrollTrack = Blend(Border, Background, 0.55f);
        ScrollThumb = Blend(Muted, Text, 0.35f);
        SecondaryButton = Blend(Border, Background, 0.25f);
        PreviewInset = Blend(Background, Border, 0.2f);
        ProgressTrack = Blend(Border, Background, 0.35f);

        SourceColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dictionary"] = Primary,
            ["Learned"] = Add,
            ["AI"] = FromHtml(theme.Colors.Accent),
            ["Grammar"] = GrammarAccent,
            ["Spelling"] = GrammarAccent,
            ["Template"] = GrammarAccent,
            ["OpenAI"] = Primary,
            ["Gemini"] = FromHtml(theme.Colors.Accent),
            ["DeepSeek"] = FromHtml(theme.Colors.Accent),
            ["Ollama"] = Add
        };

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Color SourceColor(string source)
        => SourceColors.TryGetValue(source, out var color) ? color : Muted;

    private static Color FromHtml(string hex) => ColorTranslator.FromHtml(hex);

    private static Color Blend(Color a, Color b, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var inv = 1f - amount;
        return Color.FromArgb(
            (int)(a.R * inv + b.R * amount),
            (int)(a.G * inv + b.G * amount),
            (int)(a.B * inv + b.B * amount));
    }
}
