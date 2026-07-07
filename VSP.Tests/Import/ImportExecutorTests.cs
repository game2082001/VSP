using VSP.Device.Import.Execution;
using VSP.Device.Import.Preview;
using VSP.Device.Import.Validation;
using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using Xunit;

namespace VSP.Tests.Import;

public class ImportExecutorTests
{
    [Fact]
    public void Execute_ReturnsEmptyResult_ForEmptyPreview()
    {
        var executor = new ImportExecutor(new FakeCameraRepository(), new CameraImportMapper());

        var result = executor.Execute(new ImportPreviewResult());

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(0, result.FailedRows);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Execute_ImportsSingleDevice()
    {
        var repository = new FakeCameraRepository();
        var executor = new ImportExecutor(repository, new CameraImportMapper());

        var result = executor.Execute(CreatePreviewResult(CreateValidRow(2, "Front Door")));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(0, result.FailedRows);
        var camera = Assert.Single(repository.Cameras);
        Assert.Equal("Front Door", camera.Name);
        Assert.Equal("192.168.1.2", camera.IpAddress);
    }

    [Fact]
    public void Execute_ImportsMultipleDevices()
    {
        var repository = new FakeCameraRepository();
        var executor = new ImportExecutor(repository, new CameraImportMapper());

        var result = executor.Execute(CreatePreviewResult(
            CreateValidRow(2, "Camera A"),
            CreateValidRow(3, "Camera B")));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(0, result.FailedRows);
        Assert.Equal(["Camera A", "Camera B"], repository.Cameras.Select(camera => camera.Name).ToArray());
    }

    [Fact]
    public void Execute_SkipsInvalidRows_AndCollectsErrors()
    {
        var repository = new FakeCameraRepository();
        var executor = new ImportExecutor(repository, new CameraImportMapper());

        var result = executor.Execute(CreatePreviewResult(CreateInvalidRow(5, "Broken Camera")));

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedRows);
        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(0, result.FailedRows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Broken Camera", error.Name);
        Assert.Contains("Name is required.", error.Message);
        Assert.Empty(repository.Cameras);
    }

    [Fact]
    public void Execute_ReturnsPartialFailure_WhenRepositoryThrows()
    {
        var repository = new ThrowingCameraRepository(throwOnCall: 2);
        var executor = new ImportExecutor(repository, new CameraImportMapper());

        var result = executor.Execute(CreatePreviewResult(
            CreateValidRow(2, "Camera A"),
            CreateValidRow(3, "Camera B")));

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(1, result.FailedRows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.RowNumber);
        Assert.Equal("Camera B", error.Name);
        Assert.Equal("Repository failure.", error.Message);
    }

    [Fact]
    public void Execute_ContinuesAfterRepositoryException()
    {
        var repository = new ThrowingCameraRepository(throwOnCall: 2);
        var executor = new ImportExecutor(repository, new CameraImportMapper());

        var result = executor.Execute(CreatePreviewResult(
            CreateValidRow(2, "Camera A"),
            CreateValidRow(3, "Camera B"),
            CreateValidRow(4, "Camera C")));

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.ImportedRows);
        Assert.Equal(0, result.SkippedRows);
        Assert.Equal(1, result.FailedRows);
        Assert.Equal(["Camera A", "Camera C"], repository.Cameras.Select(camera => camera.Name).ToArray());
    }

    [Fact]
    public void Execute_PreservesErrorCollection_ForSkippedAndFailedRows()
    {
        var repository = new ThrowingCameraRepository(throwOnCall: 2);
        var executor = new ImportExecutor(repository, new CameraImportMapper());

        var result = executor.Execute(CreatePreviewResult(
            CreateInvalidRow(2, "Invalid Camera"),
            CreateValidRow(3, "Camera B"),
            CreateValidRow(4, "Camera C")));

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(1, result.FailedRows);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal([2, 4], result.Errors.Select(error => error.RowNumber).ToArray());
    }

    [Fact]
    public void CameraImportMapper_MapsBrandAndConnectionType()
    {
        var mapper = new CameraImportMapper();

        var camera = mapper.Map(new ImportPreviewRow
        {
            RowNumber = 2,
            Name = "RTSP Camera",
            Brand = "RTSP",
            Model = "Model A",
            IPAddress = "192.168.1.2",
            Location = "Gate",
            IsValid = true,
            Status = "Valid"
        });

        Assert.Equal(VSP.Domain.Enums.CameraBrand.RTSP, camera.Brand);
        Assert.Equal(VSP.Domain.Enums.DeviceConnectionType.RTSP, camera.ConnectionType);
    }

    private static ImportPreviewResult CreatePreviewResult(params ImportPreviewRow[] rows)
    {
        return new ImportPreviewResult
        {
            Rows = rows,
            TotalRows = rows.Length,
            ValidRows = rows.Count(row => row.IsValid),
            InvalidRows = rows.Count(row => !row.IsValid)
        };
    }

    private static ImportPreviewRow CreateValidRow(int rowNumber, string name)
    {
        return new ImportPreviewRow
        {
            RowNumber = rowNumber,
            Name = name,
            Brand = "RTSP",
            Model = $"Model {rowNumber}",
            IPAddress = $"192.168.1.{rowNumber}",
            Location = $"Location {rowNumber}",
            IsValid = true,
            Status = "Valid"
        };
    }

    private static ImportPreviewRow CreateInvalidRow(int rowNumber, string name)
    {
        return new ImportPreviewRow
        {
            RowNumber = rowNumber,
            Name = name,
            Brand = "RTSP",
            Model = $"Model {rowNumber}",
            IPAddress = $"192.168.1.{rowNumber}",
            Location = $"Location {rowNumber}",
            IsValid = false,
            Status = "Invalid",
            Messages =
            [
                new ImportValidationMessage
                {
                    RowNumber = rowNumber,
                    FieldName = "Name",
                    Code = "Required",
                    Message = "Name is required.",
                    Severity = ImportValidationSeverity.Error
                }
            ]
        };
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        public List<Camera> Cameras { get; } = [];

        public IEnumerable<Camera> GetAll()
        {
            return Cameras;
        }

        public Camera? GetById(Guid id)
        {
            return Cameras.FirstOrDefault(camera => camera.Id == id);
        }

        public void Add(Camera camera)
        {
            Cameras.Add(camera);
        }

        public void Update(Camera camera)
        {
            throw new NotSupportedException();
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingCameraRepository : ICameraRepository
    {
        private readonly int _throwOnCall;
        private int _addCount;

        public ThrowingCameraRepository(int throwOnCall)
        {
            _throwOnCall = throwOnCall;
        }

        public List<Camera> Cameras { get; } = [];

        public IEnumerable<Camera> GetAll()
        {
            return Cameras;
        }

        public Camera? GetById(Guid id)
        {
            return Cameras.FirstOrDefault(camera => camera.Id == id);
        }

        public void Add(Camera camera)
        {
            _addCount++;

            if (_addCount == _throwOnCall)
            {
                throw new InvalidOperationException("Repository failure.");
            }

            Cameras.Add(camera);
        }

        public void Update(Camera camera)
        {
            throw new NotSupportedException();
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }
    }
}
