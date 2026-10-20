using System.Windows;
using System.Windows.Controls;
using Lexon.Core;
using Lexon.SettingsModel;

namespace Lexon.SettingsUi;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion.Current}";
        TaglineText.Text = AboutCopy.Tagline;
        CopyrightText.Text = "Copyright © 2026 Lexon. All rights reserved.";
        DeveloperNoteText.Text = AboutCopy.NoteBody;
        EmailText.Text = AboutCopy.ContactEmail;
    }

    private void OnPrivacy(object sender, RoutedEventArgs e)
        => SettingsUiHost.ShowPrivacyPolicy?.Invoke();

    private void OnTerms(object sender, RoutedEventArgs e)
        => SettingsUiHost.ShowTermsOfService?.Invoke();

    private void OnLicense(object sender, RoutedEventArgs e)
        => SettingsUiHost.ShowLicense?.Invoke();
}
