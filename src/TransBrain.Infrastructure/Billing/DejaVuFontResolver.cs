using System.Reflection;
using PdfSharp.Fonts;

namespace TransBrain.Infrastructure.Billing;

/// <summary>
/// Serves the DejaVu Sans faces embedded in this assembly to PDFsharp. PDFsharp has no
/// portable font source of its own: without a resolver, rendering works on a Windows dev
/// box and dies on the Linux CI runner. Every requested family maps to DejaVu, so MigraDoc
/// fallbacks (e.g. its internal defaults) cannot escape to a platform font either.
/// </summary>
internal sealed class DejaVuFontResolver : IFontResolver
{
    private const string Regular = "DejaVuSans";
    private const string Bold = "DejaVuSans-Bold";

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        => new(bold ? Bold : Regular, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[] GetFont(string faceName)
    {
        string resource = $"TransBrain.Infrastructure.Billing.Fonts.{faceName}.ttf";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded font '{resource}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
