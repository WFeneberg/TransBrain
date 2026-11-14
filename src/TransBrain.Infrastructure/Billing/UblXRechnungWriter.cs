using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;

namespace TransBrain.Infrastructure.Billing;

/// <summary>
/// Writes an invoice as XRechnung 3.0 — the German profile of EN 16931 — in UBL 2.1 syntax.
/// The XML is built by hand: the structure is a fixed tree over data this aggregate already
/// carries, and a schema-generated object model would be a far bigger dependency than the
/// document warrants. The seller party comes from the tenant's <see cref="InvoiceDesign"/>,
/// because each tenant is its own legal entity. Buyer reference (BT-10) falls back to the
/// customer id because TransBrain has no Leitweg-ID field; B2B recipients routinely accept
/// such references.
/// </summary>
internal sealed class UblXRechnungWriter : IXRechnungWriter
{
    private static readonly XNamespace Inv = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public byte[] Write(Invoice invoice, InvoiceDesign design)
    {
        var root = new XElement(
            Inv + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            new XElement(
                Cbc + "CustomizationID",
                "urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0"),
            new XElement(Cbc + "ProfileID", "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0"),
            new XElement(Cbc + "ID", invoice.InvoiceNumber.Value),
            new XElement(Cbc + "IssueDate", FormatDate(invoice.IssuedAt!.Value)),
            invoice.DueDate is { } dueDate ? new XElement(Cbc + "DueDate", FormatDate(dueDate)) : null,
            new XElement(Cbc + "InvoiceTypeCode", "380"),
            new XElement(Cbc + "DocumentCurrencyCode", "EUR"),
            new XElement(Cbc + "BuyerReference", invoice.CustomerId.ToString("D")),
            SupplierParty(design),
            CustomerParty(invoice),
            PaymentMeans(design),
            new XElement(
                Cac + "PaymentTerms",
                new XElement(Cbc + "Note", $"Zahlbar innerhalb von {invoice.PaymentTermDays} Tagen ohne Abzug.")),
            TaxTotal(invoice),
            MonetaryTotal(invoice));

        root.Add(invoice.Lines.OrderBy(line => line.Position).Select(line => InvoiceLineElement(line, invoice)));

        return Serialize(root);
    }

    private static XElement SupplierParty(InvoiceDesign design) => new(
        Cac + "AccountingSupplierParty",
        new XElement(
            Cac + "Party",
            new XElement(Cbc + "EndpointID", new XAttribute("schemeID", "EM"), design.Email),
            new XElement(
                Cac + "PostalAddress",
                new XElement(Cbc + "StreetName", design.Street),
                new XElement(Cbc + "CityName", design.City),
                new XElement(Cbc + "PostalZone", design.PostalCode),
                new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode", design.CountryCode))),
            new XElement(
                Cac + "PartyTaxScheme",
                new XElement(Cbc + "CompanyID", design.VatId),
                new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT"))),
            new XElement(
                Cac + "PartyLegalEntity",
                new XElement(Cbc + "RegistrationName", design.CompanyName)),
            new XElement(
                Cac + "Contact",
                new XElement(Cbc + "Name", design.CompanyName),
                design.Phone is { } phone ? new XElement(Cbc + "Telephone", phone) : null,
                new XElement(Cbc + "ElectronicMail", design.Email))));

    private static XElement CustomerParty(Invoice invoice) => new(
        Cac + "AccountingCustomerParty",
        new XElement(
            Cac + "Party",
            // BT-49: scheme 9930 is "DE:VAT"; the customer's VAT id is the only electronic
            // address TransBrain knows. Without one, the element is omitted.
            invoice.CustomerVatId is { } endpointVatId
                ? new XElement(Cbc + "EndpointID", new XAttribute("schemeID", "9930"), endpointVatId)
                : null,
            new XElement(
                Cac + "PostalAddress",
                new XElement(Cbc + "StreetName", invoice.BillingAddress.Street),
                new XElement(Cbc + "CityName", invoice.BillingAddress.City),
                new XElement(Cbc + "PostalZone", invoice.BillingAddress.PostalCode),
                new XElement(
                    Cac + "Country",
                    new XElement(Cbc + "IdentificationCode", invoice.BillingAddress.Country))),
            invoice.CustomerVatId is { } vatId
                ? new XElement(
                    Cac + "PartyTaxScheme",
                    new XElement(Cbc + "CompanyID", vatId),
                    new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))
                : null,
            new XElement(
                Cac + "PartyLegalEntity",
                new XElement(Cbc + "RegistrationName", invoice.CustomerName))));

    private static XElement PaymentMeans(InvoiceDesign design) => new(
        Cac + "PaymentMeans",
        new XElement(Cbc + "PaymentMeansCode", "58"), // SEPA credit transfer
        new XElement(
            Cac + "PayeeFinancialAccount",
            new XElement(Cbc + "ID", design.Iban),
            design.BankName is { } bank ? new XElement(Cbc + "Name", bank) : null,
            design.Bic is { } bic
                ? new XElement(Cac + "FinancialInstitutionBranch", new XElement(Cbc + "ID", bic))
                : null));

    private static XElement TaxTotal(Invoice invoice) => new(
        Cac + "TaxTotal",
        Amount(Cbc + "TaxAmount", invoice.VatAmount),
        new XElement(
            Cac + "TaxSubtotal",
            Amount(Cbc + "TaxableAmount", invoice.NetTotal),
            Amount(Cbc + "TaxAmount", invoice.VatAmount),
            TaxCategory(invoice)));

    private static XElement MonetaryTotal(Invoice invoice) => new(
        Cac + "LegalMonetaryTotal",
        Amount(Cbc + "LineExtensionAmount", invoice.NetTotal),
        Amount(Cbc + "TaxExclusiveAmount", invoice.NetTotal),
        Amount(Cbc + "TaxInclusiveAmount", invoice.GrossTotal),
        Amount(Cbc + "PayableAmount", invoice.GrossTotal));

    private static XElement InvoiceLineElement(InvoiceLine line, Invoice invoice) => new(
        Cac + "InvoiceLine",
        new XElement(Cbc + "ID", line.Position.ToString(CultureInfo.InvariantCulture)),
        new XElement(
            Cbc + "InvoicedQuantity",
            new XAttribute("unitCode", "C62"),
            "1"),
        Amount(Cbc + "LineExtensionAmount", line.NetAmount),
        new XElement(
            Cac + "Item",
            new XElement(Cbc + "Description", $"Transportauftrag {line.OrderNumber}"),
            new XElement(Cbc + "Name", line.Description),
            TaxCategory(invoice, Cac + "ClassifiedTaxCategory")),
        new XElement(Cac + "Price", Amount(Cbc + "PriceAmount", line.NetAmount)));

    /// <summary>Category S for a positive rate; Z (zero rated) when the invoice bills 0 %.</summary>
    private static XElement TaxCategory(Invoice invoice, XName? name = null) => new(
        name ?? Cac + "TaxCategory",
        new XElement(Cbc + "ID", invoice.VatRatePercent > 0m ? "S" : "Z"),
        new XElement(Cbc + "Percent", FormatAmount(invoice.VatRatePercent)),
        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")));

    private static XElement Amount(XName name, decimal value) =>
        new(name, new XAttribute("currencyID", "EUR"), FormatAmount(value));

    private static string FormatAmount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static byte[] Serialize(XElement root)
    {
        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(
                   stream,
                   new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }
}
