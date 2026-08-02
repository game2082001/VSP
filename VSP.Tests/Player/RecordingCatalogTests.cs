using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

public class RecordingCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vsp-recording-catalog-test-{Guid.NewGuid():N}");

    [Fact]
    public void ListRecordings_NoDirectory_ReturnsEmpty()
    {
        var recordings = RecordingCatalog.ListRecordings(_directory);

        Assert.Empty(recordings);
    }

    [Fact]
    public void ListRecordings_IgnoresNonMp4Files()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "not a recording");
        File.WriteAllBytes(Path.Combine(_directory, "20260729_140000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4"), []);

        var recordings = RecordingCatalog.ListRecordings(_directory);

        Assert.Single(recordings);
        Assert.EndsWith(".mp4", recordings[0].FilePath);
    }

    [Fact]
    public void ListRecordings_ParsesTimestampFromFileNamePrefix()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "20260729_143000_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.mp4"), []);

        var recordings = RecordingCatalog.ListRecordings(_directory);

        Assert.Single(recordings);
        Assert.Equal(new DateTime(2026, 7, 29, 14, 30, 0), recordings[0].RecordedAt);
    }

    [Fact]
    public void ListRecordings_OrdersNewestFirst()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "20260729_100000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4"), []);
        File.WriteAllBytes(Path.Combine(_directory, "20260729_120000_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.mp4"), []);
        File.WriteAllBytes(Path.Combine(_directory, "20260729_110000_cccccccccccccccccccccccccccccccc.mp4"), []);

        var recordings = RecordingCatalog.ListRecordings(_directory);

        Assert.Equal(3, recordings.Count);
        Assert.Equal(new DateTime(2026, 7, 29, 12, 0, 0), recordings[0].RecordedAt);
        Assert.Equal(new DateTime(2026, 7, 29, 11, 0, 0), recordings[1].RecordedAt);
        Assert.Equal(new DateTime(2026, 7, 29, 10, 0, 0), recordings[2].RecordedAt);
    }

    [Fact]
    public void ListRecordings_UnparseableFileName_FallsBackToLastWriteTime()
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, "not-a-timestamp.mp4");
        File.WriteAllBytes(filePath, []);

        var recordings = RecordingCatalog.ListRecordings(_directory);

        Assert.Single(recordings);
        Assert.Equal(File.GetLastWriteTime(filePath), recordings[0].RecordedAt);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
