using VSP.UI.ViewModels;
using ExecutionImportError = VSP.Device.Import.Execution.ImportError;
using ExecutionImportResult = VSP.Device.Import.Execution.ImportResult;
using Xunit;

namespace VSP.Tests.Import;

public class ImportSummaryViewModelTests
{
    [Fact]
    public void Constructor_MapsSuccessResult()
    {
        var viewModel = new ImportSummaryViewModel(new ExecutionImportResult
        {
            TotalRows = 3,
            ImportedRows = 3,
            SkippedRows = 0,
            FailedRows = 0,
            Errors = []
        });

        Assert.Equal(3, viewModel.TotalRows);
        Assert.Equal(3, viewModel.ImportedRows);
        Assert.Equal(0, viewModel.SkippedRows);
        Assert.Equal(0, viewModel.FailedRows);
        Assert.Empty(viewModel.Errors);
    }

    [Fact]
    public void Constructor_MapsPartialFailure()
    {
        var viewModel = new ImportSummaryViewModel(new ExecutionImportResult
        {
            TotalRows = 4,
            ImportedRows = 2,
            SkippedRows = 1,
            FailedRows = 1,
            Errors =
            [
                new ExecutionImportError { RowNumber = 2, Name = "Camera A", Message = "Invalid row." },
                new ExecutionImportError { RowNumber = 3, Name = "Camera B", Message = "Repository failure." }
            ]
        });

        Assert.Equal(4, viewModel.TotalRows);
        Assert.Equal(2, viewModel.ImportedRows);
        Assert.Equal(1, viewModel.SkippedRows);
        Assert.Equal(1, viewModel.FailedRows);
        Assert.Equal(2, viewModel.Errors.Count);
    }

    [Fact]
    public void Constructor_MapsFullFailure()
    {
        var viewModel = new ImportSummaryViewModel(new ExecutionImportResult
        {
            TotalRows = 2,
            ImportedRows = 0,
            SkippedRows = 1,
            FailedRows = 1,
            Errors =
            [
                new ExecutionImportError { RowNumber = 2, Name = "Camera A", Message = "Invalid row." },
                new ExecutionImportError { RowNumber = 3, Name = "Camera B", Message = "Repository failure." }
            ]
        });

        Assert.Equal(0, viewModel.ImportedRows);
        Assert.Equal(1, viewModel.SkippedRows);
        Assert.Equal(1, viewModel.FailedRows);
        Assert.Equal(2, viewModel.Errors.Count);
    }

    [Fact]
    public void Constructor_MapsEmptyResult()
    {
        var viewModel = new ImportSummaryViewModel(null);

        Assert.Equal(0, viewModel.TotalRows);
        Assert.Equal(0, viewModel.ImportedRows);
        Assert.Equal(0, viewModel.SkippedRows);
        Assert.Equal(0, viewModel.FailedRows);
        Assert.Empty(viewModel.Errors);
    }

    [Fact]
    public void Constructor_ExposesErrorList()
    {
        var viewModel = new ImportSummaryViewModel(new ExecutionImportResult
        {
            Errors =
            [
                new ExecutionImportError { RowNumber = 5, Name = "Front Door", Message = "Repository failure." }
            ]
        });

        var error = Assert.Single(viewModel.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Front Door", error.Name);
        Assert.Equal("Repository failure.", error.Message);
    }

    [Fact]
    public void CloseCommand_RaisesRequestClose()
    {
        var viewModel = new ImportSummaryViewModel(new ExecutionImportResult());
        var closeRequested = false;
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(closeRequested);
    }
}
