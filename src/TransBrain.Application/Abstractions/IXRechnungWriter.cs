using TransBrain.Domain.Billing;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// Writes an issued invoice as an XRechnung (EN 16931 / UBL 2.1) XML document, the German
/// electronic-invoice format. Returns the UTF-8 encoded XML.
/// </summary>
public interface IXRechnungWriter
{
    byte[] Write(Invoice invoice);
}
