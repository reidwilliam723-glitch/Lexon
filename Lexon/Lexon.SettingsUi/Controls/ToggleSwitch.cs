using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Lexon.SettingsUi;

public class ToggleSwitch : ToggleButton
{
    internal const double OffX = 0;
    internal const double OnX = 20;
    private TranslateTransform? _thumbTx;
    private bool _templateReady;

    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch), new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _thumbTx = GetTemplateChild("PART_ThumbTx") as TranslateTransform;
        _templateReady = _thumbTx != null;
        SetThumbX(IsChecked == true ? OnX : OffX, animate: false);
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        SetThumbX(OnX, animate: _templateReady);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        SetThumbX(OffX, animate: _templateReady);
    }

    internal double ThumbX
        => _thumbTx == null ? 0 : (double)_thumbTx.GetValue(TranslateTransform.XProperty);

    private void SetThumbX(double to, bool animate)
    {
        if (_thumbTx == null)
        {
            return;
        }

        var from = (double)_thumbTx.GetValue(TranslateTransform.XProperty);
        _thumbTx.BeginAnimation(TranslateTransform.XProperty, null);
        _thumbTx.X = to;

        var duration = TryFindResource("ToggleDuration") is Duration found
            ? found
            : new Duration(TimeSpan.Zero);
        if (!animate || !duration.HasTimeSpan || duration.TimeSpan == TimeSpan.Zero)
        {
            return;
        }

        _thumbTx.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from, to, duration));
    }
}
