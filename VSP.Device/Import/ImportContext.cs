namespace VSP.Device.Import;

public class ImportContext
{
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";
    public string ImportOption { get; set; } = "";
    public string DuplicatePolicy { get; set; } = "";
}
