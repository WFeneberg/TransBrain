using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.ListVehicleMaintenances;

/// <summary>The vehicle's maintenance history, latest maintenance first.</summary>
public sealed record ListVehicleMaintenancesQuery(Guid VehicleId) : IQuery<VehicleMaintenanceResponse[]>;
