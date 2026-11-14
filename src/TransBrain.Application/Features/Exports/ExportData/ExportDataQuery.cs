using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Exports.ExportData;

/// <summary>
/// Builds the download file for one export target. <c>TargetEntity</c> is an
/// <see cref="Imports.ImportTargetEntity"/> name, <c>Format</c> an <see cref="ExportFormat"/> name;
/// both are matched case-insensitively.
/// </summary>
public sealed record ExportDataQuery(string TargetEntity, string Format) : IQuery<ExportFileResult>;
