namespace Lexon.SettingsModel;

public interface IAiPolicyPublisher
{
    void Publish(bool localOnly, bool typing, bool rewrite, bool prefetch);
}
