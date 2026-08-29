using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.AssignOrder;

public sealed record AssignOrderCommand(Guid TourId, Guid TransportOrderId) : ICommand<TourResponse>;
