using System.IO;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ShottrClone;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings.Clone();

        PostCaptureCombo.ItemsSource = new[]
        {
            new Choice<PostCaptureAction>("Show compact preview", PostCaptureAction.Preview),
            new Choice<PostCaptureAction>("Open editor", PostCaptureAction.Editor),
            new Choice<PostCaptureAction>("Copy only", PostCaptureAction.CopyOnly),
            new Choice<PostCaptureAction>("Save only", PostCaptureAction.SaveOnly)
        };
        PostCaptureCombo.DisplayMemberPath = nameof(Choice<PostCaptureAction>.Label);
        PostCaptureCombo.SelectedValuePath = nameof(Choice<PostCaptureAction>.Value);
        PostCaptureCombo.SelectedValue = Settings.PostCaptureAction;

        AutoCopyCheck.IsChecked = Settings.AutoCopy;
        AutoSaveCheck.IsChecked = Settings.AutoSave;
        SaveFolderText.Text = Settings.SaveFolder;
        FileNamePatternText.Text = Settings.FileNamePattern;
        ImageFormatCombo.SelectedIndex = Settings.ImageFormat == "jpg" ? 1 : 0;
        StartWithWindowsCheck.IsChecked = Settings.StartWithWindows;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose the default ScreenShottr save folder",
            SelectedPath = SaveFolderText.Text,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SaveFolderText.Text = dialog.SelectedPath;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var folder = SaveFolderText.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
        {
            MessageBox.Show(
                this,
                "Choose a valid absolute save folder.",
                "ScreenShottr Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Settings.AutoCopy = AutoCopyCheck.IsChecked == true;
        Settings.AutoSave = AutoSaveCheck.IsChecked == true;
        Settings.SaveFolder = folder;
        Settings.FileNamePattern = FileNamePatternText.Text.Trim();
        Settings.ImageFormat =
            (ImageFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "png";
        Settings.PostCaptureAction =
            PostCaptureCombo.SelectedValue is PostCaptureAction action
                ? action
                : PostCaptureAction.Preview;
        Settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        DialogResult = true;
    }

    private sealed record Choice<T>(string Label, T Value);
}
