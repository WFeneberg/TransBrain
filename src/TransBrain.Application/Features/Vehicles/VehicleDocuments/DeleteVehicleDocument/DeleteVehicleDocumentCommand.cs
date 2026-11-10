using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.DeleteVehicleDocument;

public sealed record DeleteVehicleDocumentCommand(Guid VehicleId, Guid DocumentId) : ICommand<Unit>;