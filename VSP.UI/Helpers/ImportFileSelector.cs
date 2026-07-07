using Microsoft.Win32;
using System.IO;

namespace VSP.UI.Helpers;

public class ImportFileSelector
{
    private readonly Func<ImportFileSelection?> _selectFile;

    public ImportFileSelector(Func<ImportFileSelection?>? selectFile = null)
    {
        _selectFile = selectFile ?? ShowDialog;
    }

    public ImportFileSelection? SelectFile()
    {
        return _selectFile();
    }

    private static ImportFileSelection? ShowDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Import File",
            Filter = "Import Files (*.csv;*.xlsx)|*.csv;*.xlsx|CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        return new ImportFileSelection(dialog.FileName, Path.GetExtension(dialog.FileName));
    }
}
