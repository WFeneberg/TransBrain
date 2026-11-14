using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;

namespace TransBrain.Infrastructure.Billing;

/// <summary>
/// Renders an issued invoice as a printable German A4 PDF via MigraDoc/PDFsharp (MIT-licensed,
/// unlike QuestPDF and friends). Layout and letterhead come from the tenant's
/// <see cref="InvoiceDesign"/>: company data, logo, accent colour, base font size and the free
/// header/footer texts the invoice designer edits. The structure stays a freight invoice —
/// sender block, recipient, meta table, line table, totals, payment instruction — not a brochure.
/// </summary>
internal sealed class MigraDocInvoicePdfRenderer : IInvoicePdfRenderer
{
    private const string FontFamily = "DejaVu Sans";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    static MigraDocInvoicePdfRenderer()
    {
        // PDFsharp allows setting the resolver only once per process; tests and app share it.
        GlobalFontSettings.FontResolver ??= new DejaVuFontResolver();
    }

    public byte[] Render(Invoice invoice, InvoiceDesign design)
    {
        Document document = Build(invoice, design);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static Document Build(Invoice invoice, InvoiceDesign design)
    {
        var document = new Document();
        Style normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontFamily;
        normal.Font.Size = design.FontSizePoints;

        Section section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        Color accent = ParseAccent(design.AccentColor, out Color onAccent);

        AddHeader(section, design, accent);
        AddAddressAndMeta(section, invoice);
        AddTitle(section, invoice, accent);
        AddHeaderText(section, design);
        AddLines(section, invoice, accent, onAccent);
        AddTotals(section, invoice);
        AddPaymentNote(section, invoice, design);
        AddFooterText(section, design);
        AddFooter(section, design);

        return document;
    }

    private static void AddHeader(Section section, InvoiceDesign design, Color accent)
    {
        if (design.Logo is not null)
        {
            Table table = section.AddTable();
            table.Borders.Width = 0;
            table.AddColumn(Unit.FromCentimeter(10.5));
            table.AddColumn(Unit.FromCentimeter(6));
            Row row = table.AddRow();

            int nameIndex = design.LogoPosition == InvoiceLogoPosition.Left ? 1 : 0;
            int logoIndex = 1 - nameIndex;

            Paragraph name = row.Cells[nameIndex].AddParagraph(design.CompanyName);
            StyleCompanyName(name, design, accent);
            name.Format.Alignment = nameIndex == 1 ? ParagraphAlignment.Right : ParagraphAlignment.Left;

            Paragraph logoHolder = row.Cells[logoIndex].AddParagraph();
            logoHolder.Format.Alignment = logoIndex == 1 ? ParagraphAlignment.Right : ParagraphAlignment.Left;
            // MigraDoc reads images from pseudo file names; "base64:" keeps the logo out of
            // the file system entirely.
            Image image = logoHolder.AddImage("base64:" + Convert.ToBase64String(design.Logo));
            image.Height = Unit.FromCentimeter(1.8);
            image.LockAspectRatio = true;
        }
        else
        {
            Paragraph name = section.AddParagraph(design.CompanyName);
            StyleCompanyName(name, design, accent);
        }

        // Absenderzeile above the recipient, as a window envelope expects it.
        Paragraph senderLine = section.AddParagraph(
            $"{design.CompanyName} · {design.Street} · {design.PostalCode} {design.City}");
        senderLine.Format.Font.Size = 7;
        senderLine.Format.Font.Color = Colors.Gray;
        senderLine.Format.SpaceBefore = Unit.FromCentimeter(1);
        senderLine.Format.SpaceAfter = Unit.FromMillimeter(2);
    }

    private static void StyleCompanyName(Paragraph name, InvoiceDesign design, Color accent)
    {
        name.Format.Font.Size = design.FontSizePoints + 6;
        name.Format.Font.Bold = true;
        name.Format.Font.Color = accent;
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

    private static void AddTitle(Section section, Invoice invoice, Color accent)
    {
        Paragraph title = section.AddParagraph($"Rechnung {invoice.InvoiceNumber.Value}");
        title.Format.Font.Size = 13;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = accent;
        title.Format.SpaceBefore = Unit.FromCentimeter(1.2);
        title.Format.SpaceAfter = Unit.FromMillimeter(4);
    }

    private static void AddHeaderText(Section section, InvoiceDesign design)
    {
        if (design.HeaderText is null)
        {
            return;
        }

        Paragraph paragraph = section.AddParagraph();
        paragraph.Format.SpaceAfter = Unit.FromMillimeter(4);
        AddMultilineText(paragraph, design.HeaderText);
    }

    private static void AddLines(Section section, Invoice invoice, Color accent, Color onAccent)
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
        header.Format.Font.Color = onAccent;
        header.Shading.Color = accent;
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

    private static void AddPaymentNote(Section section, Invoice invoice, InvoiceDesign design)
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
            $"Bitte überweisen Sie den Rechnungsbetrag{due} ohne Abzug auf IBAN {design.Iban}"
            + (design.Bic is { } bic ? $", BIC {bic}" : string.Empty)
            + (design.BankName is { } bank ? $" ({bank})" : string.Empty)
            + $". Geben Sie dabei die Rechnungsnummer {invoice.InvoiceNumber.Value} an.");
    }

    private static void AddFooterText(Section section, InvoiceDesign design)
    {
        if (design.FooterText is null)
        {
            return;
        }

        Paragraph paragraph = section.AddParagraph();
        paragraph.Format.SpaceBefore = Unit.FromMillimeter(6);
        AddMultilineText(paragraph, design.FooterText);
    }

    private static void AddFooter(Section section, InvoiceDesign design)
    {
        Paragraph footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Colors.Gray;
        footer.Format.Alignment = ParagraphAlignment.Center;

        List<string> parts =
        [
            $"{design.CompanyName} · {design.Street} · {design.PostalCode} {design.City}",
            $"USt-IdNr. {design.VatId}",
        ];
        if (design.TaxNumber is { } taxNumber)
        {
            parts.Add($"Steuernr. {taxNumber}");
        }

        parts.Add(design.Email);
        if (design.Phone is { } phone)
        {
            parts.Add($"Tel. {phone}");
        }

        footer.AddText(string.Join(" · ", parts));
    }

    /// <summary>The designer's free texts may span lines; MigraDoc needs explicit breaks.</summary>
    private static void AddMultilineText(Paragraph paragraph, string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.AddLineBreak();
            }

            if (lines[i].Length > 0)
            {
                paragraph.AddText(lines[i]);
            }
        }
    }

    /// <summary>
    /// The accent colour plus a readable text colour on top of it, chosen by perceived
    /// luminance: table headers over a pale accent need black text, over a dark one white.
    /// </summary>
    private static Color ParseAccent(string hex, out Color onAccent)
    {
        byte r = Convert.ToByte(hex.Substring(1, 2), 16);
        byte g = Convert.ToByte(hex.Substring(3, 2), 16);
        byte b = Convert.ToByte(hex.Substring(5, 2), 16);

        double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        onAccent = luminance > 0.6 ? Colors.Black : Colors.White;

        return new Color(r, g, b);
    }

    private static string FormatMoney(decimal amount) => amount.ToString("N2", German) + " €";

    private static string FormatDate(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("dd.MM.yyyy", German);

    private static string FormatDate(DateOnly date) => date.ToString("dd.MM.yyyy", German);
}
