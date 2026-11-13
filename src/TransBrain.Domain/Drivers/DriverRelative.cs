using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

/// <summary>
/// A relative of a driver ("Verwandte"), typically the emergency contact. The
/// <see cref="Sequence"/> keeps the order the user entered them in; it is assigned by
/// <see cref="Driver"/> and starts at 1.
/// </summary>
public sealed record DriverRelative
{
    public const int MaxNameLength = 100;
    public const int MaxRelationshipLength = 100;
    public const int MaxPhoneLength = 50;
    public const int MinYear = 1900;

    private DriverRelative(
        int sequence,
        string lastName,
        string? firstName,
        string? relationship,
        DateOnly? dateOfBirth,
        string? phone)
    {
        Sequence = sequence;
        LastName = lastName;
        FirstName = firstName;
        Relationship = relationship;
        DateOfBirth = dateOfBirth;
        Phone = phone;
    }

    public int Sequence { get; }

    /// <summary>Family name ("Name").</summary>
    public string LastName { get; }

    /// <summary>Given name ("Vorname").</summary>
    public string? FirstName { get; }

    /// <summary>Degree of kinship ("Ver.Grad"), free text as in the legacy system.</summary>
    public string? Relationship { get; }

    /// <summary>Date of birth ("Geb.-Dat").</summary>
    public DateOnly? DateOfBirth { get; }

    /// <summary>Phone number ("Telefon").</summary>
    public string? Phone { get; }

    public static Result<DriverRelative> Create(
        string lastName,
        string? firstName = null,
        string? relationship = null,
        DateOnly? dateOfBirth = null,
        string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Error.Validation(
                "DriverRelative.LastNameRequired", "The relative's family name must not be empty.");
        }

        if (dateOfBirth is { Year: < MinYear })
        {
            return Error.Validation(
                "DriverRelative.DateOfBirthTooEarly",
                $"The relative's date of birth must not be before {MinYear}.");
        }

        string trimmedLastName = lastName.Trim();
        string? normalizedFirstName = OptionalText.Normalize(firstName);
        string? normalizedRelationship = OptionalText.Normalize(relationship);
        string? normalizedPhone = OptionalText.Normalize(phone);

        if (trimmedLastName.Length > MaxNameLength
            || normalizedFirstName is { Length: > MaxNameLength }
            || normalizedRelationship is { Length: > MaxRelationshipLength }
            || normalizedPhone is { Length: > MaxPhoneLength })
        {
            return Error.Validation(
                "DriverRelative.TextTooLong",
                $"A relative's names must not exceed {MaxNameLength} characters, the degree of "
                + $"kinship {MaxRelationshipLength}, the phone number {MaxPhoneLength}.");
        }

        return new DriverRelative(
            0, trimmedLastName, normalizedFirstName, normalizedRelationship, dateOfBirth, normalizedPhone);
    }

    internal DriverRelative WithSequence(int sequence) =>
        new(sequence, LastName, FirstName, Relationship, DateOfBirth, Phone);
}
