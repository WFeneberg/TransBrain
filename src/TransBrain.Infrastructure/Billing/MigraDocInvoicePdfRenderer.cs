using System.Globalization;
using Microsoft.Extensions.Options;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;

namespace TransBrain.Infrastructure.Billing;

/// <summary>
/// Renders an issued invoice as a printable German A4 PDF via MigraDoc/PDFsharp (MIT-licensed,
/// unlike QuestPDF and friends). The document is deliberately plain: sender block, recipient,
/// meta table, line table, totals, payment instruction — a freight invoice, not a brochure.
/// </summary>
internal sealed class MigraDocInvoicePdfRenderer(IOptions<InvoiceSellerOptions> options) : IInvoicePdfRenderer
{
    private const string FontFamily = "DejaVu Sans";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    static MigraDocInvoicePdfRenderer()
    {
        // PDFsharp allows setting the resolver only once per process; tests and app share it.
        GlobalFontSettings.FontResolver ??= new DejaVuFontResolver();
    }

    public byte[] Render(Invoice invoice)
    {
        InvoiceSellerOptions seller = options.Value;
        Document document = Build(invoice, seller);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static Document Build(Invoice invoice, InvoiceSellerOptions seller)
    {
        var document = new Document();
        Style normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontFamily;
        normal.Font.Size = 10;

        Section section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        AddHeader(section, seller);
        AddAddressAndMeta(section, invoice);
        AddTitle(section, invoice);
        AddLines(section, invoice);
        AddTotals(section, invoice);
        AddPaymentNote(section, invoice, seller);
        AddFooter(section, seller);

        return document;
    }

    private static void AddHeader(Section section, InvoiceSellerOptions seller)
    {
        Paragraph name = section.AddParagraph(seller.Name);
        name.Format.Font.Size = 16;
        name.Format.Font.Bold = true;
        name.Format.SpaceAfter = Unit.FromCentimeter(1);

        // Absenderzeile above the recipient, as a window envelope expects it.
        Paragraph senderLine = section.AddParagraph(
            $"{seller.Name} · {seller.Street} · {seller.PostalCode} {seller.City}");
        senderLine.Format.Font.Size = 7;
        senderLine.Format.Font.Color = Colors.Gray;
        senderLine.Format.SpaceAfter = Unit.FromMillimeter(2);
    }

    private static void AddAddressAndMeta(Section section, Invoice invoice)
    {
        Table table = section.AddTable();
        table.Borders.Width = 0;
        table.AddColumn(Unit.FromCentimeter(9));
        table.AddColumn(Unit.FromCentimeter(7.5));
        Row row = table.AddRow();

        Cell recipient = row.Cells[0];
        recipient.AddParagraph(invoice.CustomerName).Format.Font.Bold = true;
        recipient.AddParagraph(invoice.BillingAddress.Street);
        recipient.AddParagraph($"{invoice.BillingAddress.PostalCode} {invoice.BillingAddress.City}");
        recipient.AddParagraph(invoice.BillingAddress.Country);

        Cell meta = row.Cells[1];
        AddMetaLine(meta, "Rechnungsnummer", invoice.InvoiceNumber.Value, bold: true);
        if (invoice.IssuedAt is { } issuedAt)
        {
            AddMetaLine(meta, "Rechnungsdatum", FormatDate(issuedAt));
        }

        if (invoice.DueDate is { } dueDate)
        {
            AddMetaLine(meta, "Fällig am", FormatDate(dueDate));
        }

        AddMetaLine(meta, "Zahlungsziel", $"{invoice.PaymentTermDays} Tage");
        if (invoice.CustomerVatId is { } customerVatId)
        {
            AddMetaLine(meta, "USt-IdNr. Kunde", customerVatId);
        }

        if (invoice.PaidAt is { } paidAt)
        {
            AddMetaLine(meta, "Bezahlt am", FormatDate(paidAt));
        }
    }

    private static void AddMetaLine(Cell cell, string label, string value, bool bold = false)
    {
        Paragraph paragraph = cell.AddParagraph();
        paragraph.Format.Alignment = ParagraphAlignment.Right;
        paragraph.AddText($"{label}: ");
        FormattedText text = paragraph.AddFormattedText(value);
        text.Bold = bold;
    }

    private static void AddTitle(Section section, Invoice invoice)
    {
        Paragraph title = section.AddParagraph($"Rechnung {invoice.InvoiceNumber.Value}");
        title.Format.Font.Size = 13;
        title.Format.Font.Bold = true;
        title.Format.SpaceBefore = Unit.FromCentimeter(1.2);
        title.Format.SpaceAfter = Unit.FromMillimeter(4);
    }

    private static void AddLines(Section section, Invoice invoice)
    {
        Table table = section.AddTable();
        table.Borders.Width = 0.25;
        table.Borders.Color = Colors.Gray;

        table.AddColumn(Unit.FromCentimeter(1.2)).Format.Alignment = ParagraphAlignment.Right;
        table.AddColumn(Unit.FromCentimeter(3.6));
        table.AddColumn(Unit.FromCentimeter(8.2));
        table.AddColumn(Unit.FromCentimeter(3.5)).Format.Alignment = ParagraphAlignment.Right;

        Row header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Shading.Color = Colors.WhiteSmoke;
        header.Cells[0].AddParagraph("Pos.");
        header.Cells[1].AddParagraph("Auftrag");
        header.Cells[2].AddParagraph("Leistung");
        header.Cells[3].AddParagraph("Netto");

        foreach (InvoiceLine line in invoice.Lines.OrderBy(l => l.Position))
        {
            Row row = table.AddRow();
            row.Cells[0].AddParagraph(line.Position.ToString(German));
            row.Cells[1].AddParagraph(line.OrderNumber);
            row.Cells[2].AddParagraph(line.Description);
            row.Cells[3].AddParagraph(FormatMoney(line.NetAmount));
        }
    }

    private static void AddTotals(Section section, Invoice invoice)
    {
        Table table = section.AddTable();
        table.Borders.Width = 0;
        table.Format.SpaceBefore = Unit.FromMillimeter(4);
        table.AddColumn(Unit.FromCentimeter(13)).Format.Alignment = ParagraphAlignment.Right;
        table.AddColumn(Unit.FromCentimeter(3.5)).Format.Alignment = ParagraphAlignment.Right;

        AddTotalRow(table, "Nettobetrag", FormatMoney(invoice.NetTotal), bold: false);
        AddTotalRow(
            table,
            $"Umsatzsteuer ({invoice.VatRatePercent.ToString("0.##", German)} %)",
            FormatMoney(invoice.VatAmount),
            bold: false);
        AddTotalRow(table, "Rechnungsbetrag", FormatMoney(invoice.GrossTotal), bold: true);
    }

    private static void AddTotalRow(Table table, string label, string value, bool bold)
    {
        Row row = table.AddRow();
        row.Format.Font.Bold = bold;
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(value);
    }

    private static void AddPaymentNote(Section section, Invoice invoice, InvoiceSellerOptions seller)
    {
        Paragraph note = section.AddParagraph();
        note.Format.SpaceBefore = Unit.FromCentimeter(1);

        if (invoice.Status == InvoiceStatus.Paid && invoice.PaidAt is { } paidAt)
        {
            note.AddText($"Der Rechnungsbetrag wurde am {FormatDate(paidAt)} beglichen. Vielen Dank.");
            return;
        }

        string due = invoice.DueDate is { } dueDate ? $" bis zum {FormatDate(dueDate)}" : string.Empty;
        note.AddText(
            $"Bitte überweisen Sie den Rechnungsbetrag{due} ohne Abzug auf IBAN {seller.Iban}"
            + (seller.Bic is { } bic ? $", BIC {bic}" : string.Empty)
            + (seller.BankName is { } bank ? $" ({bank})" : string.Empty)
            + $". Geben Sie dabei die Rechnungsnummer {invoice.InvoiceNumber.Value} an.");
    }

    private static void AddFooter(Section section, InvoiceSellerOptions seller)
    {
        Paragraph footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Colors.Gray;
        footer.Format.Alignment = ParagraphAlignment.Center;

        List<string> parts =
        [
            $"{seller.Name} · {seller.Street} · {seller.PostalCode} {seller.City}",
            $"USt-IdNr. {seller.VatId}",
        ];
        if (seller.TaxNumber is { } taxNumber)
        {
            parts.Add($"Steuernr. {taxNumber}");
        }

        parts.Add(seller.Email);
        if (seller.Phone is { } phone)
        {
            parts.Add($"Tel. {phone}");
        }

        footer.AddText(string.Join(" · ", parts));
    }

    private static string FormatMoney(decimal amount) => amount.ToString("N2", German) + " €";

    private static string FormatDate(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("dd.MM.yyyy", German);

    private static string FormatDate(DateOnly date) => date.ToString("dd.MM.yyyy", German);
}
