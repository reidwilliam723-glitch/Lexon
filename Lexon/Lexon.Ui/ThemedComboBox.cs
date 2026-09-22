namespace Lexon.Ui;

/// <summary>
/// Owner-drawn combo boxes erase their background on every WM_PAINT, so the
/// closed box flashes the form colour before DrawItem fills it. Swallowing
/// WM_ERASEBKGND keeps the last frame until the new one is drawn.
/// </summary>
public sealed class ThemedComboBox : ComboBox
{
    private const int WmEraseBkgnd = 0x0014;

    public ThemedComboBox()
    {
        IntegralHeight = false;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, false);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmEraseBkgnd)
        {
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }
}
