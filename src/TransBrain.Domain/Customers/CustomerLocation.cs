using TransBrain.Domain.Common;

namespace TransBrain.Domain.Customers;

/// <summary>
/// A place a customer delivers to, with its own address and contact person. A customer can have
/// many of these; each one is independent of the customer's registered address.
/// </summary>
public sealed class CustomerLocation
{
    // EF Core materialization only. Every other construction goes through Create.
    private CustomerLocation()
    {
        Name = null!;
        Address = null!;
    }

    private CustomerLocation(
        Guid id,
        Guid customerId,
        string name,
        Address address,
        string? contactPerson,
        string? email,
        string? phone)
    {
        Id = id;
        CustomerId = customerId;
        Name = name;
        Address = address;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; }

    public Address Address { get; private set; }

    public string? ContactPerson { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public static Result<CustomerLocation> Create(
        Guid customerId,
        string name,
        Address address,
        string? contactPerson,
        string? email,
        string? phone)
    {
        Result<Unit> validation = ValidateName(name);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        return new CustomerLocation(
            Guid.CreateVersion7(),
            customerId,
            name.Trim(),
            address,
            OptionalText.Normalize(contactPerson),
            OptionalText.Normalize(email),
            OptionalText.Normalize(phone));
    }

    public Result<CustomerLocation> Update(
        string name,
        Address address,
        string? contactPerson,
        string? email,
        string? phone)
    {
        Result<Unit> validation = ValidateName(name);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        Name = name.Trim();
        Address = address;
        ContactPerson = OptionalText.Normalize(contactPerson);
        Email = OptionalText.Normalize(email);
        Phone = OptionalText.Normalize(phone);

        return this;
    }

    private static Result<Unit> ValidateName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? Error.Validation("CustomerLocation.NameRequired", "Location name must not be empty.")
            : Unit.Value;
}
