using System.Text;
using VSP.Device.Import;
using VSP.Device.Import.Execution;
using VSP.Device.Import.Preview;
using VSP.Device.Import.Validation;
using VSP.Device.Interfaces;
using EntityCamera = VSP.Domain.Entities.Camera;
using VSP.UI.Helpers;
using VSP.UI.ViewModels;
using Xunit;

namespace VSP.Tests.Import;

public class ImportWizardViewModelTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    [Fact]
    public void BrowseFileCommand_LoadsEmptyPreview_ForEmptyCsv()
    {
        var filePath = CreateTempFile(".csv", string.Empty);
        var viewModel = CreateViewModel(filePath, ".csv");

        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal(Path.GetFileName(filePath), viewModel.SelectedFileName);
        Assert.Equal(".csv", viewModel.SelectedFileType);
        Assert.Equal(0, viewModel.TotalRows);
        Assert.Equal(0, viewModel.ValidRows);
        Assert.Equal(0, viewModel.InvalidRows);
        Assert.Empty(viewModel.PreviewRows);
        Assert.Equal("Step 3 - Ready to Import", viewModel.CurrentStep);
        Assert.Equal("Preview loaded. No rows found.", viewModel.StatusMessage);
    }

    [Fact]
    public void BrowseFileCommand_LoadsPreviewRows_AndSummary()
    {
        var filePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://front,Gate
            Lobby,RTSP,Model B,192.168.1.11,80,554,8000,admin,1234,RTSP,rtsp://lobby,Hall
            """);
        var viewModel = CreateViewModel(filePath, ".csv");

        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal(2, viewModel.TotalRows);
        Assert.Equal(2, viewModel.ValidRows);
        Assert.Equal(0, viewModel.InvalidRows);
        Assert.Equal(2, viewModel.PreviewRows.Count);
        Assert.Equal([2, 3], viewModel.PreviewRows.Select(row => row.RowNumber).ToArray());
        Assert.Equal("Front Door", viewModel.PreviewRows[0].Name);
        Assert.Equal("Valid", viewModel.PreviewRows[0].Status);
        Assert.Equal("Preview loaded successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public void BrowseFileCommand_PreservesInvalidAndDuplicatePreviewRows()
    {
        var filePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Camera A,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://shared,Gate
            Camera A,RTSP,Model B,invalid-ip,80,554,8000,admin,1234,RTSP,rtsp://shared,Hall
            """);
        var viewModel = CreateViewModel(filePath, ".csv");

        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal(2, viewModel.TotalRows);
        Assert.Equal(0, viewModel.ValidRows);
        Assert.Equal(2, viewModel.InvalidRows);
        Assert.Equal(2, viewModel.PreviewRows.Count);
        Assert.All(viewModel.PreviewRows, row => Assert.Equal("Invalid", row.Status));
        Assert.Contains(viewModel.PreviewRows[0].Messages, message => message.Code == "DuplicateName");
        Assert.Contains(viewModel.PreviewRows[0].Messages, message => message.Code == "DuplicateRtspUrl");
        Assert.Contains(viewModel.PreviewRows[1].Messages, message => message.Code == "InvalidIpAddress");
    }

    [Fact]
    public void RefreshCommand_ReloadsCurrentlySelectedFile_WithoutBrowsingAgain()
    {
        var filePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://front,Gate
            """);
        var browseCount = 0;
        var viewModel = CreateViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper()),
            new ImportFileSelector(() =>
            {
                browseCount++;
                return new ImportFileSelection(filePath, ".csv");
            }));

        viewModel.BrowseFileCommand.Execute(null);
        File.WriteAllText(
            filePath,
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://front,Gate
            Lobby,RTSP,Model B,192.168.1.11,80,554,8000,admin,1234,RTSP,rtsp://lobby,Hall
            """,
            Encoding.UTF8);

        viewModel.RefreshCommand.Execute(null);

        Assert.Equal(1, browseCount);
        Assert.Equal(2, viewModel.TotalRows);
        Assert.Equal(2, viewModel.PreviewRows.Count);
        Assert.Equal("Lobby", viewModel.PreviewRows[1].Name);
    }

    [Fact]
    public void BrowseFileCommand_SetsFriendlyMessage_WhenSelectionCancelled()
    {
        var viewModel = CreateViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper()),
            new ImportFileSelector(() => null));

        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal("File selection cancelled.", viewModel.StatusMessage);
        Assert.Empty(viewModel.PreviewRows);
    }

    [Fact]
    public void BrowseFileCommand_ShowsFriendlyMessage_ForUnsupportedFileType()
    {
        var filePath = CreateTempFile(".json", "{}");
        var viewModel = CreateViewModel(filePath, ".json");

        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal("Unsupported file type.", viewModel.StatusMessage);
        Assert.Equal("Step 1 - Select File", viewModel.CurrentStep);
        Assert.Empty(viewModel.PreviewRows);
        Assert.Equal(0, viewModel.TotalRows);
        Assert.Equal(0, viewModel.ValidRows);
        Assert.Equal(0, viewModel.InvalidRows);
    }

    [Fact]
    public void BrowseFileCommand_ShowsFriendlyMessage_WhenPipelineThrows()
    {
        var filePath = CreateTempFile(".csv", "ignored");
        var viewModel = CreateViewModel(
            new ImportPipelineService(
                parsers: [new ThrowingParser()],
                validationEngine: new ImportValidationEngine(),
                duplicateChecker: new DuplicateChecker()),
            new ImportPreviewBuilder(),
            new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper()),
            new ImportFileSelector(() => new ImportFileSelection(filePath, ".csv")));

        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal("Step 1 - Select File", viewModel.CurrentStep);
        Assert.StartsWith("Import preview failed: Parser failure.", viewModel.StatusMessage);
        Assert.Empty(viewModel.PreviewRows);
    }

    [Fact]
    public void CloseCommand_RaisesCloseRequested()
    {
        var viewModel = CreateViewModel(CreateTempFile(".csv", string.Empty), ".csv");
        var closeRequested = false;
        viewModel.CloseRequested += () => closeRequested = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(closeRequested);
    }

    [Fact]
    public void ImportCommand_IsDisabled_UntilValidPreviewExists()
    {
        var emptyViewModel = CreateViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper()),
            new ImportFileSelector(() => null));

        Assert.False(emptyViewModel.ImportCommand.CanExecute(null));

        var invalidFilePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Camera A,RTSP,Model A,invalid-ip,80,554,8000,admin,1234,RTSP,rtsp://shared,Gate
            """);
        var invalidViewModel = CreateViewModel(invalidFilePath, ".csv");
        invalidViewModel.BrowseFileCommand.Execute(null);

        Assert.False(invalidViewModel.ImportCommand.CanExecute(null));

        var validFilePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://front,Gate
            """);
        var validViewModel = CreateViewModel(validFilePath, ".csv");
        validViewModel.BrowseFileCommand.Execute(null);

        Assert.True(validViewModel.ImportCommand.CanExecute(null));
    }

    [Fact]
    public void ImportCommand_UpdatesStatus_AfterImportCompletes()
    {
        var filePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://front,Gate
            """);
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(repository, new CameraImportMapper()),
            new ImportFileSelector(() => new ImportFileSelection(filePath, ".csv")));

        viewModel.BrowseFileCommand.Execute(null);
        viewModel.ImportCommand.Execute(null);

        Assert.Equal("Import completed. Imported: 1, Skipped: 0, Failed: 0.", viewModel.StatusMessage);
        Assert.Single(repository.Cameras);
    }

    [Fact]
    public void ImportCommand_RaisesImportCompleted()
    {
        var filePath = CreateTempFile(
            ".csv",
            """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,1234,RTSP,rtsp://front,Gate
            """);
        var viewModel = CreateViewModel(filePath, ".csv");
        VSP.Device.Import.Execution.ImportResult? completedResult = null;
        viewModel.ImportCompleted += result => completedResult = result;

        viewModel.BrowseFileCommand.Execute(null);
        viewModel.ImportCommand.Execute(null);

        Assert.NotNull(completedResult);
        Assert.Equal(1, completedResult.ImportedRows);
        Assert.Equal(0, completedResult.FailedRows);
    }

    [Fact]
    public void RefreshCommand_IsDisabled_UntilAFileIsSelected()
    {
        var viewModel = CreateViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper()),
            new ImportFileSelector(() => null));

        Assert.False(viewModel.RefreshCommand.CanExecute(null));

        viewModel.BrowseFileCommand.Execute(null);
        Assert.False(viewModel.RefreshCommand.CanExecute(null));

        var filePath = CreateTempFile(".csv", string.Empty);
        var loadedViewModel = CreateViewModel(filePath, ".csv");
        loadedViewModel.BrowseFileCommand.Execute(null);

        Assert.True(loadedViewModel.RefreshCommand.CanExecute(null));
    }

    private static ImportWizardViewModel CreateViewModel(string filePath, string fileType)
    {
        return CreateViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper()),
            new ImportFileSelector(() => new ImportFileSelection(filePath, fileType)));
    }

    private static ImportWizardViewModel CreateViewModel(
        ImportPipelineService pipelineService,
        ImportPreviewBuilder previewBuilder,
        ImportExecutor importExecutor,
        ImportFileSelector fileSelector)
    {
        return new ImportWizardViewModel(pipelineService, previewBuilder, importExecutor, fileSelector);
    }

    private string CreateTempFile(string extension, string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(filePath, content, Encoding.UTF8);
        _temporaryFiles.Add(filePath);
        return filePath;
    }

    public void Dispose()
    {
        foreach (var filePath in _temporaryFiles)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private sealed class ThrowingParser : IImportParser
    {
        public string ParserName => "Throwing";

        public bool CanParse(string fileType)
        {
            return string.Equals(fileType, ".csv", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<ImportRow> Parse(Stream stream)
        {
            throw new InvalidOperationException("Parser failure.");
        }
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        public List<EntityCamera> Cameras { get; } = [];

        public IEnumerable<EntityCamera> GetAll()
        {
            return Cameras;
        }

        public EntityCamera? GetById(Guid id)
        {
            return Cameras.FirstOrDefault(camera => camera.Id == id);
        }

        public void Add(EntityCamera camera)
        {
            Cameras.Add(camera);
        }

        public void Update(EntityCamera camera)
        {
            throw new NotSupportedException();
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }
    }
}
