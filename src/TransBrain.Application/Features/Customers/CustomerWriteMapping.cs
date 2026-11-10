using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Customers;

/// <summary>Turns the wire values of a customer write into domain values.</summary>
internal static class CustomerWriteMapping
{
    /// <param name="current">The customer's additional data before an update; null when a customer is created.</param>
    public static async Task<Result<CustomerAdditionalInfo>> ToAdditionalAsync(
        CustomerAdditionalData? data,
        IVehicleCatalogRepository catalog,
        CustomerAdditionalInfo? current,
        CancellationToken cancellationToken)
    {
        if (data is null)
        {
            return CustomerAdditionalInfo.Empty;
        }

        // Same dotted key FluentValidation produces for the nested rules, so clients map both alike.
        Result<string?> customerClass = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.CustomerClass,
            data.CustomerClass,
            current?.CustomerClass,
            $"Additional.{nameof(CustomerAdditionalData.CustomerClass)}",
            cancellationToken);
        if (!customerClass.IsSuccess)
        {
            return customerClass.Error!;
        }

        return CustomerAdditionalInfo.Create(
            data.SecondaryName,
            data.SecondaryStreet,
            data.SecondaryPostalCode,
            data.SecondaryCity,
            data.SecondaryCountry,
            data.FoundedOn,
            data.WorkerCount,
            data.SalariedEmployeeCount,
            data.RequiresPalletCheck,
            data.ExcludeTollFromInvoice,
            data.ReimburseToll,
            customerClass.Value,
            data.Comment);
    }
}
