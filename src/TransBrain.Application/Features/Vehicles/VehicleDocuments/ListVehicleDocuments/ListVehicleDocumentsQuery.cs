using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.ListVehicleDocuments;

/// <summary>The vehicle's special documents, soonest expiry first; documents without expiry come last.</summary>
public sealed record ListVehicleDocumentsQuery(Guid VehicleId) : IQuery<VehicleDocumentResponse[]>;