using Lexon.Core;

namespace Lexon.SettingsModel;

public interface ILexonCheck
{
    LexonStatusSnapshot Capture();
}
