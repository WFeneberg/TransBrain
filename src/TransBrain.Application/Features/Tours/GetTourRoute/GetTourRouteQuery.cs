using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.GetTourRoute;

/// <param name="Departure">
/// Start of the first loading. Defaults to 06:00 on the tour date, in the dispatch time zone.
/// </param>
public sealed record GetTourRouteQuery(Guid Id, DateTimeOffset? Departure) : IQuery<TourRouteResponse>;
