using Microsoft.Win32;

namespace VSP.UI.Helpers;

public class ExportFileSelector
{
    private readonly Func<string?> _selectFile;

    public ExportFileSelector(Func<string?>? selectFile = null)
    {
        _selectFile = selectFile ?? ShowDialog;
    }

    public string? SelectFile()
    {
        return _selectFile();
    }

    private static string? ShowDialog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Cameras",
            Filter = "CSV Files (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = "cameras.csv"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
