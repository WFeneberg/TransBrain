using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.GetTourById;

public sealed record GetTourByIdQuery(Guid Id) : IQuery<TourResponse>;
