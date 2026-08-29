using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.CompleteTour;

public sealed record CompleteTourCommand(Guid TourId) : ICommand<TourResponse>;
