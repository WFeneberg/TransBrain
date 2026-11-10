using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers;

/// <summary>
/// Wire shape of <see cref="CustomerAdditionalInfo"/>, used in both the customer write bodies and
/// <see cref="CustomerResponse"/>. <see cref="CustomerClass"/> is a code of the customer class catalog.
/// </summary>
public sealed record CustomerAdditionalData(
    string? SecondaryName = null,
    string? SecondaryStreet = null,
    string? SecondaryPostalCode = null,
    string? SecondaryCity = null,
    string? SecondaryCountry = null,
    DateOnly? FoundedOn = null,
    int WorkerCount = 0,
    int SalariedEmployeeCount = 0,
    bool RequiresPalletCheck = false,
    bool ExcludeTollFromInvoice = false,
    bool ReimburseToll = false,
    string? CustomerClass = null,
    string? Comment = null)
{
    public static CustomerAdditionalData From(CustomerAdditionalInfo info) => new(
        info.SecondaryName,
        info.SecondaryStreet,
        info.SecondaryPostalCode,
        info.SecondaryCity,
        info.SecondaryCountry,
        info.FoundedOn,
        info.WorkerCount,
        info.SalariedEmployeeCount,
        info.RequiresPalletCheck,
        info.ExcludeTollFromInvoice,
        info.ReimburseToll,
        info.CustomerClass,
        info.Comment);
}
