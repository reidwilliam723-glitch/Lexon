using System.Windows.Controls.Primitives;

namespace Lexon.SettingsUi;

public class ToggleSwitch : ToggleButton
{
    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch), new System.Windows.FrameworkPropertyMetadata(typeof(ToggleSwitch)));
    }
}
