using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    /// The one piece of this screen with no branching logic worth unit-testing (Epic-016 §4) --
    /// the actual <see cref="System.Windows.Forms.FolderBrowserDialog"/> call lives here in
    /// code-behind rather than in <see cref="SettingsViewModel"/>. Sets RecordingPath on
    /// selection, leaves it unchanged on cancel.
    /// </summary>
    private void BrowseRecordingPathButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();

        if (Directory.Exists(_viewModel.RecordingPath))
        {
            dialog.SelectedPath = _viewModel.RecordingPath;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _viewModel.RecordingPath = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// Epic-017 Backup destination picker. <see cref="SaveFileDialog.OverwritePrompt"/> (default
    /// <c>true</c>) is the entire implementation of "existing backup files require overwrite
    /// confirmation" -- no custom code needed. Returns <c>null</c> on cancel.
    /// </summary>
    internal static string? ChooseBackupDestination()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Backup Database",
            FileName = $"VSP_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
            Filter = "VSP Database Backup (*.db)|*.db|All Files (*.*)|*.*",
            DefaultExt = ".db",
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Epic-017 Restore source picker. Returns <c>null</c> on cancel.</summary>
    internal static string? ChooseRestoreSource()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Restore Database",
            Filter = "VSP Database Backup (*.db)|*.db|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
