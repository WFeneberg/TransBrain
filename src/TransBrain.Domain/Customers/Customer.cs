using TransBrain.Domain.Common;

namespace TransBrain.Domain.Customers;

public sealed class Customer
{
    private const int MaxPaymentTermDays = 365;

    // EF Core materialization only. Every other construction goes through Create.
    private Customer()
    {
        Name = null!;
        Address = null!;
    }

    private Customer(
        Guid id,
        string name,
        Address address,
        string? contactPerson,
        string? email,
        string? phone,
        string? vatId,
        int paymentTermDays)
    {
        Id = id;
        Name = name;
        Address = address;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
        VatId = vatId;
        PaymentTermDays = paymentTermDays;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Address Address { get; private set; }

    public string? ContactPerson { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>Umsatzsteuer-Identifikationsnummer (UID), for example <c>ATU12345678</c>.</summary>
    public string? VatId { get; private set; }

    /// <summary>Days from invoice date until payment is due. Zero means due immediately.</summary>
    public int PaymentTermDays { get; private set; }

    public static Result<Customer> Create(
        string name,
        Address address,
        string? contactPerson,
        string? email,
        string? phone,
        string? vatId,
        int paymentTermDays)
    {
        Result<Unit> validation = Validate(name, paymentTermDays);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        return new Customer(
            Guid.CreateVersion7(),
            name.Trim(),
            address,
            OptionalText.Normalize(contactPerson),
            OptionalText.Normalize(email),
            OptionalText.Normalize(phone),
            OptionalText.Normalize(vatId),
            paymentTermDays);
    }

    public Result<Customer> Update(
        string name,
        Address address,
        string? contactPerson,
        string? email,
        string? phone,
        string? vatId,
        int paymentTermDays)
    {
        Result<Unit> validation = Validate(name, paymentTermDays);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        Name = name.Trim();
        Address = address;
        ContactPerson = OptionalText.Normalize(contactPerson);
        Email = OptionalText.Normalize(email);
        Phone = OptionalText.Normalize(phone);
        VatId = OptionalText.Normalize(vatId);
        PaymentTermDays = paymentTermDays;

        return this;
    }

    private static Result<Unit> Validate(string name, int paymentTermDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("Customer.NameRequired", "Name must not be empty.");
        }

        if (paymentTermDays is < 0 or > MaxPaymentTermDays)
        {
            return Error.Validation(
                "Customer.PaymentTermDaysOutOfRange",
                $"Payment term must be between 0 and {MaxPaymentTermDays} days.");
        }

        return Unit.Value;
    }
}
