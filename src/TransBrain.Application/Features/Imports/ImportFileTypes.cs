using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports;

/// <summary>Maps uploaded file extensions to the import source type that parses them.</summary>
public static class ImportFileTypes
{
    private static readonly Dictionary<string, ImportSourceType> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = ImportSourceType.Csv,
        [".txt"] = ImportSourceType.Csv,
        [".json"] = ImportSourceType.Json,
        [".xml"] = ImportSourceType.Xml,
        [".xlsx"] = ImportSourceType.Excel,
        [".mdb"] = ImportSourceType.Access,
        [".accdb"] = ImportSourceType.Access,
        [".sqlite"] = ImportSourceType.Sqlite,
        [".sqlite3"] = ImportSourceType.Sqlite,
        [".db"] = ImportSourceType.Sqlite
    };

    public static IReadOnlyCollection<string> SupportedExtensions => ByExtension.Keys;

    public static ImportSourceType? Resolve(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return ByExtension.TryGetValue(extension, out ImportSourceType type) ? type : null;
    }
}
