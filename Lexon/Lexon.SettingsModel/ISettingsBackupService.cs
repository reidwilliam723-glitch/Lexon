namespace Lexon.SettingsModel;

/// <summary>
/// Export/import profile settings and app rules (not learning data).
/// </summary>
public interface ISettingsBackupService
{
    string ExportJson(bool includePersonalData = false);

    bool ImportJson(string json, bool overwrite = true);
}
