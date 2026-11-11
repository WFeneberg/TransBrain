using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Returns a fixed byte payload and records which invoice was rendered.</summary>
internal sealed class StubInvoicePdfRenderer : IInvoicePdfRenderer
{
    public Invoice? Rendered { get; private set; }

    public byte[] Render(Invoice invoice)
    {
        Rendered = invoice;
        return [0x25, 0x50, 0x44, 0x46];
    }
}

/// <summary>Returns a fixed byte payload and records which invoice was written.</summary>
internal sealed class StubXRechnungWriter : IXRechnungWriter
{
    public Invoice? Written { get; private set; }

    public byte[] Write(Invoice invoice)
    {
        Written = invoice;
        return [0x3C, 0x3F, 0x78, 0x6D];
    }
}
