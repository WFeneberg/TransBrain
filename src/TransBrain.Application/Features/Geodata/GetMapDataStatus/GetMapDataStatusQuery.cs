using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Geodata.GetMapDataStatus;

public sealed record GetMapDataStatusQuery : IQuery<MapDataStatusResponse>;
