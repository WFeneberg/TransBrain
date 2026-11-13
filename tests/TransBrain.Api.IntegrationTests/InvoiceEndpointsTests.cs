using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Customers.CustomerLocations;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Api.IntegrationTests;

public class InvoiceEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    // Tour dates are unique per test for the same reason as in TourEndpointsTests: the
    // (tour_date, vehicle_id) and (tour_date, driver_id) unique indexes are global.
    private static int _plateCounter;
    private static int _dayCounter;

    private static string NextPlate() => $"M-RE {3000 + Interlocked.Increment(ref _plateCounter)}";

    private static DateOnly NextTourDate() =>
        DateOnly.FromDayNumber(new DateOnly(2097, 1, 1).DayNumber + Interlocked.Increment(ref _dayCounter));

    private async Task<CustomerResponse> CreateCustomerAsync(string name)
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/customers", new
        {
            name,
            street = "Rechnungsweg 1",
            postalCode = "80331",
            city = "München",
            country = "DE",
            contactPerson = "Anna Muster",
            email = "billing@kunde.de",
            phone = (string?)null,
            vatId = "DE123456789",
            paymentTermDays = 14
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    /// <summary>Runs an order through the whole dispatch lifecycle so it ends up Delivered.</summary>
    private async Task<OrderResponse> CreateDeliveredOrderAsync(string consignorName)
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");

        HttpResponseMessage vehicleResponse = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/vehicles", new
            {
                licensePlate = NextPlate(),
                type = "Tractor",
                payloadKg = 18_000,
                loadMeters = 13.6m,
                nextInspectionDue = "2028-03-31"
            });
        vehicleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        VehicleResponse vehicle = (await vehicleResponse.Content.ReadFromJsonAsync<VehicleResponse>())!;

        HttpResponseMessage driverResponse = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/drivers", new
            {
                firstName = "Frank",
                lastName = $"Abrechnung{Interlocked.Increment(ref _plateCounter)}",
                licenseClasses = new[] { "CE" },
                licenseValidUntil = "2099-06-30",
                externalUserId = (string?)null
            });
        driverResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        DriverResponse driver = (await driverResponse.Content.ReadFromJsonAsync<DriverResponse>())!;

        HttpResponseMessage orderResponse = await dispatcher.PostAsJsonAsync("/api/orders", new
        {
            consignor = new
            {
                name = consignorName, street = "Hauptstr. 1", postalCode = "80331",
                city = "München", country = "DE"
            },
            consignee = new
            {
                name = "Empfänger AG", street = "Bahnhofstr. 2", postalCode = "10115",
                city = "Berlin", country = "DE"
            },
            cargoDescription = "Palettenware",
            cargoWeightKg = 5_000,
            cargoLoadMeters = 4.0m,
            pickupFrom = "2027-03-01T08:00:00+00:00",
            pickupTo = "2027-03-01T10:00:00+00:00",
            deliveryFrom = "2027-03-01T12:00:00+00:00",
            deliveryTo = "2027-03-01T16:00:00+00:00"
        });
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        OrderResponse order = (await orderResponse.Content.ReadFromJsonAsync<OrderResponse>())!;

        HttpResponseMessage tourResponse = await dispatcher.PostAsJsonAsync("/api/tours", new
        {
            tourDate = NextTourDate().ToString("yyyy-MM-dd"),
            vehicleId = vehicle.Id,
            driverId = driver.Id
        });
        tourResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        TourResponse tour = (await tourResponse.Content.ReadFromJsonAsync<TourResponse>())!;

        (await dispatcher.PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await dispatcher.PostAsync($"/api/tours/{tour.Id}/start", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await dispatcher.PostAsync($"/api/tours/{tour.Id}/complete", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return order;
    }

    private async Task<InvoiceResponse> CreateInvoiceAsync(Guid customerId, Guid orderId, decimal netAmount = 1_000m)
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/invoices", new
        {
            customerId,
            vatRatePercent = 19m,
            lines = new[] { new { orderId, netAmount, description = (string?)null } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task PostInvoice_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().PostAsJsonAsync("/api/invoices", new
        {
            customerId = Guid.CreateVersion7(),
            vatRatePercent = 19m,
            lines = new[] { new { orderId = Guid.CreateVersion7(), netAmount = 100m, description = (string?)null } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostInvoice_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").PostAsJsonAsync("/api/invoices", new
        {
            customerId = Guid.CreateVersion7(),
            vatRatePercent = 19m,
            lines = new[] { new { orderId = Guid.CreateVersion7(), netAmount = 100m, description = (string?)null } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostInvoice_ForADeliveredOrder_CreatesADraftWithComputedTotals()
    {
        CustomerResponse customer = await CreateCustomerAsync("Rechnungskunde GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Entwurf GmbH");

        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id, netAmount: 1_234.56m);

        invoice.InvoiceNumber.Should().StartWith("RE-");
        invoice.Status.Should().Be("Draft");
        invoice.CustomerName.Should().Be("Rechnungskunde GmbH");
        invoice.Lines.Should().ContainSingle().Which.OrderNumber.Should().Be(order.OrderNumber);
        invoice.NetTotal.Should().Be(1_234.56m);
        invoice.VatAmount.Should().Be(234.57m);
        invoice.GrossTotal.Should().Be(1_469.13m);
        invoice.DueDate.Should().BeNull();

        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/invoices?pageSize=100");
        PagedResult<InvoiceResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<InvoiceResponse>>();
        page!.Items.Should().Contain(i => i.Id == invoice.Id);
    }

    [Fact]
    public async Task PostInvoice_BilledToALocationWithOwnCompanyName_UsesTheLocationAsRecipient()
    {
        CustomerResponse customer = await CreateCustomerAsync("Konzern Mutter GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Werkslieferung GmbH");

        HttpResponseMessage locationResponse = await factory.CreateClientAs("admin")
            .PostAsJsonAsync($"/api/customers/{customer.Id}/locations", new
            {
                name = "Werk Berlin",
                street = "Bahnhofstr. 2",
                postalCode = "10115",
                city = "Berlin",
                country = "DE",
                companyName = "Werk Berlin GmbH",
                vatId = "DE987654321"
            });
        locationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid locationId = (await locationResponse.Content.ReadFromJsonAsync<CustomerLocationResponse>())!.Id;

        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/invoices", new
        {
            customerId = customer.Id,
            vatRatePercent = 19m,
            lines = new[] { new { orderId = order.Id, netAmount = 500m, description = (string?)null } },
            billToLocationId = locationId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        InvoiceResponse invoice = (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        invoice.BillToLocationId.Should().Be(locationId);
        invoice.CustomerName.Should().Be("Werk Berlin GmbH");
        invoice.BillingAddress.City.Should().Be("Berlin");
        invoice.CustomerVatId.Should().Be("DE987654321");
        invoice.CustomerId.Should().Be(customer.Id);
    }

    [Fact]
    public async Task PostInvoice_BilledToALocationWithoutCompanyName_ReturnsConflict()
    {
        CustomerResponse customer = await CreateCustomerAsync("Nur Lieferort GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Lagerlieferung GmbH");

        HttpResponseMessage locationResponse = await factory.CreateClientAs("admin")
            .PostAsJsonAsync($"/api/customers/{customer.Id}/locations", new
            {
                name = "Lager Nord",
                street = "Hafenstr. 9",
                postalCode = "20095",
                city = "Hamburg",
                country = "DE"
            });
        Guid locationId = (await locationResponse.Content.ReadFromJsonAsync<CustomerLocationResponse>())!.Id;

        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/invoices", new
        {
            customerId = customer.Id,
            vatRatePercent = 19m,
            lines = new[] { new { orderId = order.Id, netAmount = 500m, description = (string?)null } },
            billToLocationId = locationId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostInvoice_ForAnUndeliveredOrder_ReturnsConflict()
    {
        CustomerResponse customer = await CreateCustomerAsync("Zufrueh GmbH");
        HttpResponseMessage orderResponse = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/orders", new
        {
            consignor = new
            {
                name = "Absender GmbH", street = "Hauptstr. 1", postalCode = "80331",
                city = "München", country = "DE"
            },
            consignee = new
            {
                name = "Empfänger AG", street = "Bahnhofstr. 2", postalCode = "10115",
                city = "Berlin", country = "DE"
            },
            cargoDescription = "Palettenware",
            cargoWeightKg = 5_000,
            cargoLoadMeters = 4.0m,
            pickupFrom = "2027-03-01T08:00:00+00:00",
            pickupTo = "2027-03-01T10:00:00+00:00",
            deliveryFrom = "2027-03-01T12:00:00+00:00",
            deliveryTo = "2027-03-01T16:00:00+00:00"
        });
        OrderResponse draftOrder = (await orderResponse.Content.ReadFromJsonAsync<OrderResponse>())!;

        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/invoices", new
        {
            customerId = customer.Id,
            vatRatePercent = 19m,
            lines = new[] { new { orderId = draftOrder.Id, netAmount = 100m, description = (string?)null } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invoice.OrderNotDelivered");
    }

    [Fact]
    public async Task PostInvoice_SameOrderTwice_ReturnsConflict()
    {
        CustomerResponse customer = await CreateCustomerAsync("Doppelt GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("DoppeltOrder GmbH");
        await CreateInvoiceAsync(customer.Id, order.Id);

        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/invoices", new
        {
            customerId = customer.Id,
            vatRatePercent = 19m,
            lines = new[] { new { orderId = order.Id, netAmount = 200m, description = (string?)null } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invoice.OrderAlreadyBilled");
    }

    [Fact]
    public async Task GetBillableOrders_AfterInvoicing_NoLongerListsTheOrder()
    {
        CustomerResponse customer = await CreateCustomerAsync("Abrechenbar GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Abrechenbar Order GmbH");
        HttpClient viewer = factory.CreateClientAs("viewer");

        HttpResponseMessage before = await viewer.GetAsync("/api/invoices/billable-orders");
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        (await before.Content.ReadFromJsonAsync<List<OrderResponse>>())!
            .Should().Contain(o => o.Id == order.Id);

        await CreateInvoiceAsync(customer.Id, order.Id);

        HttpResponseMessage after = await viewer.GetAsync("/api/invoices/billable-orders");
        (await after.Content.ReadFromJsonAsync<List<OrderResponse>>())!
            .Should().NotContain(o => o.Id == order.Id);
    }

    [Fact]
    public async Task PostIssueAndPay_WalksTheInvoiceThroughItsLifecycle()
    {
        CustomerResponse customer = await CreateCustomerAsync("Lebenszyklus GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Lebenszyklus Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id);
        HttpClient dispatcher = factory.CreateClientAs("disponent");

        HttpResponseMessage issued = await dispatcher.PostAsync($"/api/invoices/{invoice.Id}/issue", null);
        issued.StatusCode.Should().Be(HttpStatusCode.OK, await issued.Content.ReadAsStringAsync());
        InvoiceResponse issuedInvoice = (await issued.Content.ReadFromJsonAsync<InvoiceResponse>())!;
        issuedInvoice.Status.Should().Be("Issued");
        issuedInvoice.IssuedAt.Should().NotBeNull();
        // 14 days of payment terms were snapshotted from the customer at creation time.
        issuedInvoice.DueDate.Should().Be(
            DateOnly.FromDateTime(issuedInvoice.IssuedAt!.Value.UtcDateTime).AddDays(14));

        HttpResponseMessage paid = await dispatcher.PostAsync($"/api/invoices/{invoice.Id}/pay", null);
        paid.StatusCode.Should().Be(HttpStatusCode.OK, await paid.Content.ReadAsStringAsync());
        (await paid.Content.ReadFromJsonAsync<InvoiceResponse>())!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task PostPay_OnADraft_ReturnsConflict()
    {
        CustomerResponse customer = await CreateCustomerAsync("Vorschnell GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Vorschnell Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id);

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsync($"/api/invoices/{invoice.Id}/pay", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invoice.InvalidTransition");
    }

    [Fact]
    public async Task PostCancel_FreesTheOrderForBillingAgain()
    {
        CustomerResponse customer = await CreateCustomerAsync("Storno GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("Storno Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id);
        HttpClient dispatcher = factory.CreateClientAs("disponent");

        HttpResponseMessage cancelled = await dispatcher.PostAsync($"/api/invoices/{invoice.Id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());
        (await cancelled.Content.ReadFromJsonAsync<InvoiceResponse>())!.Status.Should().Be("Cancelled");

        HttpResponseMessage billable = await dispatcher.GetAsync("/api/invoices/billable-orders");
        (await billable.Content.ReadFromJsonAsync<List<OrderResponse>>())!
            .Should().Contain(o => o.Id == order.Id);

        // And the order can actually be invoiced a second time.
        InvoiceResponse second = await CreateInvoiceAsync(customer.Id, order.Id);
        second.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetInvoiceById_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/invoices/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExportInvoicePdf_IssuedInvoice_ReturnsPdfFile()
    {
        CustomerResponse customer = await CreateCustomerAsync("PDF Export GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("PDF Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id);
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        (await dispatcher.PostAsync($"/api/invoices/{invoice.Id}/issue", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Viewers may download documents: exporting is a read, not a billing action.
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/invoices/{invoice.Id}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain(".pdf");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1_000);
        bytes.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');
    }

    [Fact]
    public async Task ExportInvoicePdf_DraftInvoice_ReturnsConflict()
    {
        CustomerResponse customer = await CreateCustomerAsync("PDF Entwurf GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("PDF Entwurf Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id);

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .GetAsync($"/api/invoices/{invoice.Id}/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invoice.NotADocument");
    }

    [Fact]
    public async Task ExportInvoiceXRechnung_IssuedInvoice_ReturnsUblXml()
    {
        CustomerResponse customer = await CreateCustomerAsync("XRechnung Export GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("XRechnung Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id, netAmount: 500m);
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        (await dispatcher.PostAsync($"/api/invoices/{invoice.Id}/issue", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/invoices/{invoice.Id}/xrechnung");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");

        System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Parse(await response.Content.ReadAsStringAsync());
        System.Xml.Linq.XNamespace cbc =
            "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        doc.Root!.Name.LocalName.Should().Be("Invoice");
        doc.Root.Element(cbc + "ID")!.Value.Should().Be(invoice.InvoiceNumber);
        doc.Root.Element(cbc + "CustomizationID")!.Value.Should().Contain("xrechnung_3.0");
        doc.ToString().Should().Contain("TransBrain Logistik GmbH");
        doc.ToString().Should().Contain("XRechnung Export GmbH");
        doc.ToString().Should().Contain("595.00");
    }

    [Fact]
    public async Task ExportInvoiceXRechnung_DraftInvoice_ReturnsConflict()
    {
        CustomerResponse customer = await CreateCustomerAsync("XRechnung Entwurf GmbH");
        OrderResponse order = await CreateDeliveredOrderAsync("XRechnung Entwurf Order GmbH");
        InvoiceResponse invoice = await CreateInvoiceAsync(customer.Id, order.Id);

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .GetAsync($"/api/invoices/{invoice.Id}/xrechnung");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invoice.NotADocument");
    }
}
