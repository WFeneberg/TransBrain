using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.AddVehicleDocument;

/// <remarks>The rules are shared with the update; see <see cref="VehicleDocumentWriteRules"/>.</remarks>
public sealed class AddVehicleDocumentCommandValidator : AbstractValidator<AddVehicleDocumentCommand>
{
    public AddVehicleDocumentCommandValidator()
    {
        Include(new VehicleDocumentWriteRules());
    }
}