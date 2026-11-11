using TransBrain.Domain.Common;

namespace TransBrain.Domain.Customers;

/// <summary>
/// A contact person at a delivery location, with their own phone and e-mail. The
/// <see cref="Sequence"/> keeps the order the user entered them in; it is assigned by
/// <see cref="CustomerLocation"/> and starts at 1.
/// </summary>
public sealed record CustomerLocationContact
{
    public const int MaxNameLength = 200;
    public const int MaxEmailLength = 320;
    public const int MaxPhoneLength = 50;

    private CustomerLocationContact(int sequence, string name, string? phone, string? email)
    {
        Sequence = sequence;
        Name = name;
        Phone = phone;
        Email = email;
    }

    public int Sequence { get; }

    public string Name { get; }

    public string? Phone { get; }

    public string? Email { get; }

    public static Result<CustomerLocationContact> Create(string name, string? phone, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation(
                "CustomerLocationContact.NameRequired", "The contact person's name must not be empty.");
        }

        return new CustomerLocationContact(
            0, name.Trim(), OptionalText.Normalize(phone), OptionalText.Normalize(email));
    }

    internal CustomerLocationContact WithSequence(int sequence) => new(sequence, Name, Phone, Email);
}
