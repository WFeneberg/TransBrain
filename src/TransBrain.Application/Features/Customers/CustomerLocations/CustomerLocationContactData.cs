using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations;

/// <summary>Wire shape of <see cref="CustomerLocationContact"/>, used in location write bodies and responses.</summary>
public sealed record CustomerLocationContactData(string Name, string? Phone = null, string? Email = null)
{
    public static CustomerLocationContactData From(CustomerLocationContact contact) =>
        new(contact.Name, contact.Phone, contact.Email);

    /// <summary>
    /// Turns the wire contacts into domain contacts. A missing list means "no contacts". Field
    /// problems are already caught by the command validators, so a failure here is only the
    /// domain's own backstop.
    /// </summary>
    public static Result<List<CustomerLocationContact>> ToDomain(IReadOnlyList<CustomerLocationContactData>? contacts)
    {
        var result = new List<CustomerLocationContact>();
        foreach (CustomerLocationContactData data in contacts ?? [])
        {
            Result<CustomerLocationContact> contact = CustomerLocationContact.Create(data.Name, data.Phone, data.Email);
            if (!contact.IsSuccess)
            {
                return contact.Error!;
            }

            result.Add(contact.Value);
        }

        return result;
    }
}
