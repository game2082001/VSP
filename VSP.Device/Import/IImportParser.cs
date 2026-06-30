using System.IO;

namespace VSP.Device.Import;

public interface IImportParser
{
    string ParserName { get; }
    bool CanParse(string fileType);
    IEnumerable<ImportRow> Parse(Stream stream);
}
