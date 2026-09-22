namespace Lexon.Ui;

/// <summary>
/// Batches child paints into one frame. Native combo boxes still paint as
/// their own HWNDs; compositing the host is what stops three of them flashing
/// in sequence when they invalidate together.
/// </summary>
public class CompositedTableLayoutPanel : TableLayoutPanel
{
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }
}
