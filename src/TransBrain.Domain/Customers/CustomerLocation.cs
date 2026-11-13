using TransBrain.Domain.Common;

namespace TransBrain.Domain.Customers;

/// <summary>
/// A place a customer delivers to, with its own address and contact persons. A customer can have
/// many of these; each one is independent of the customer's registered address. A location with
/// its own company name is a distinct legal recipient and can receive its own invoices.
/// </summary>
public sealed class CustomerLocation
{
    public const int MaxContacts = 20;

    private readonly List<CustomerLocationContact> _contacts = [];

    // EF Core materialization only. Every other construction goes through Create.
    private CustomerLocation()
    {
        Name = null!;
        Address = null!;
    }

    private CustomerLocation(
        Guid id, Guid customerId, string name, Address address, string? companyName, string? vatId)
    {
        Id = id;
        CustomerId = customerId;
        Name = name;
        Address = address;
        CompanyName = companyName;
        VatId = vatId;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; }

    public Address Address { get; private set; }

    /// <summary>
    /// The location's own company name, if it is a distinct legal entity. A location with a
    /// company name can be billed directly instead of its customer.
    /// </summary>
    public string? CompanyName { get; private set; }

    /// <summary>The location's own VAT identification number (Umsatzsteuer-Identnummer).</summary>
    public string? VatId { get; private set; }

    /// <summary>The contact persons in the order they were entered.</summary>
    public IReadOnlyList<CustomerLocationContact> Contacts => _contacts.OrderBy(c => c.Sequence).ToList();

    public static Result<CustomerLocation> Create(
        Guid customerId,
        string name,
        Address address,
        IReadOnlyList<CustomerLocationContact> contacts,
        string? companyName = null,
        string? vatId = null)
    {
        Result<Unit> validation = Validate(name, contacts);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        var location = new CustomerLocation(
            Guid.CreateVersion7(),
            customerId,
            name.Trim(),
            address,
            OptionalText.Normalize(companyName),
            OptionalText.Normalize(vatId));
        location.ReplaceContacts(contacts);
        return location;
    }

    public Result<CustomerLocation> Update(
        string name,
        Address address,
        IReadOnlyList<CustomerLocationContact> contacts,
        string? companyName = null,
        string? vatId = null)
    {
        Result<Unit> validation = Validate(name, contacts);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        Name = name.Trim();
        Address = address;
        CompanyName = OptionalText.Normalize(companyName);
        VatId = OptionalText.Normalize(vatId);
        ReplaceContacts(contacts);

        return this;
    }

    private void ReplaceContacts(IReadOnlyList<CustomerLocationContact> contacts)
    {
        _contacts.Clear();
        _contacts.AddRange(contacts.Select((contact, index) => contact.WithSequence(index + 1)));
    }

    private static Result<Unit> Validate(string name, IReadOnlyList<CustomerLocationContact> contacts)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("CustomerLocation.NameRequired", "Location name must not be empty.");
        }

        if (contacts.Count > MaxContacts)
        {
            return Error.Validation(
                "CustomerLocation.TooManyContacts",
                $"A delivery location can have at most {MaxContacts} contact persons.");
        }

        return Unit.Value;
    }
}
