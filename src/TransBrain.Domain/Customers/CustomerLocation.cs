using TransBrain.Domain.Common;

namespace TransBrain.Domain.Customers;

/// <summary>
/// A place a customer delivers to, with its own address and contact persons. A customer can have
/// many of these; each one is independent of the customer's registered address.
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

    private CustomerLocation(Guid id, Guid customerId, string name, Address address)
    {
        Id = id;
        CustomerId = customerId;
        Name = name;
        Address = address;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; }

    public Address Address { get; private set; }

    /// <summary>The contact persons in the order they were entered.</summary>
    public IReadOnlyList<CustomerLocationContact> Contacts => _contacts.OrderBy(c => c.Sequence).ToList();

    public static Result<CustomerLocation> Create(
        Guid customerId,
        string name,
        Address address,
        IReadOnlyList<CustomerLocationContact> contacts)
    {
        Result<Unit> validation = Validate(name, contacts);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        var location = new CustomerLocation(Guid.CreateVersion7(), customerId, name.Trim(), address);
        location.ReplaceContacts(contacts);
        return location;
    }

    public Result<CustomerLocation> Update(
        string name,
        Address address,
        IReadOnlyList<CustomerLocationContact> contacts)
    {
        Result<Unit> validation = Validate(name, contacts);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        Name = name.Trim();
        Address = address;
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
