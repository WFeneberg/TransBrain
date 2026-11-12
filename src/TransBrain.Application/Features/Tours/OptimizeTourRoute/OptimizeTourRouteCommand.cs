using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.OptimizeTourRoute;

/// <summary>
/// Reorders the tour's stops so the estimated travel distance shrinks, keeping every delivery
/// behind its own pickup. A no-op when the current order is already the shortest found.
/// </summary>
public sealed record OptimizeTourRouteCommand(Guid Id) : ICommand<TourResponse>;
