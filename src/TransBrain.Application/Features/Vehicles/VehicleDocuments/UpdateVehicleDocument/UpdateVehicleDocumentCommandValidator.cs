using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.UpdateVehicleDocument;

/// <remarks>The rules are shared with the add; see <see cref="VehicleDocumentWriteRules"/>.</remarks>
public sealed class UpdateVehicleDocumentCommandValidator : AbstractValidator<UpdateVehicleDocumentCommand>
{
    public UpdateVehicleDocumentCommandValidator()
    {
        Include(new VehicleDocumentWriteRules());
    }
}