using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Geodata.ListGeodata;

public sealed record ListGeodataQuery : IQuery<GeodataEntryResponse[]>;
