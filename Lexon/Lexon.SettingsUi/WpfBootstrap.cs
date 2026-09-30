using System.Windows;

namespace Lexon.SettingsUi;

public static class WpfBootstrap
{
    private static bool _templatesMerged;

    public static Application EnsureApplication()
    {
        var app = Application.Current ?? new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        MergeTemplates(app);
        return app;
    }

    public static void ShowModeless(Window window)
    {
        EnsureApplication();
        window.Show();
    }

    private static void MergeTemplates(Application app)
    {
        if (_templatesMerged)
        {
            return;
        }

        _templatesMerged = true;
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Lexon.SettingsUi;component/Themes/Tokens.xaml", UriKind.Absolute)
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Lexon.SettingsUi;component/Themes/Generic.xaml", UriKind.Absolute)
        });
    }
}
