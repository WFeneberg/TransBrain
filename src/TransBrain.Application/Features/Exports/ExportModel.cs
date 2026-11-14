namespace TransBrain.Application.Features.Exports;

/// <summary>The file formats master data can be exported to.</summary>
public enum ExportFormat
{
    Csv,
    Json,
    Xlsx
}

/// <summary>One exportable entity for the export UI, with its current row count.</summary>
public sealed record ExportTargetResponse(string Entity, string Label, int RowCount);

/// <summary>A generated export file, ready to be sent as a download.</summary>
public sealed record ExportFileResult(string FileName, string ContentType, byte[] Content);
