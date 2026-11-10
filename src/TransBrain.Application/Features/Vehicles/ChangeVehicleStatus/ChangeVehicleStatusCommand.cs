using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.ChangeVehicleStatus;

/// <param name="Status">Target status: <c>Available</c>, <c>InWorkshop</c> or <c>Decommissioned</c>.</param>
public sealed record ChangeVehicleStatusCommand(Guid Id, string Status) : ICommand<VehicleResponse>;
