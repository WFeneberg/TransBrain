using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Geodata.DeleteGeodataOverride;

/// <summary>Removes a manual position; the address falls back to the geocoder.</summary>
public sealed record DeleteGeodataOverrideCommand(Guid Id) : ICommand<Unit>;
