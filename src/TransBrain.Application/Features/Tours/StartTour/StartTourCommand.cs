using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.StartTour;

public sealed record StartTourCommand(Guid TourId) : ICommand<TourResponse>;
