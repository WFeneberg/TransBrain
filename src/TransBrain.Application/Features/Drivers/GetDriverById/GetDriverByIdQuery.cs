using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.GetDriverById;

public sealed record GetDriverByIdQuery(Guid Id) : IQuery<DriverResponse>;
