using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.RemoveOrder;

public sealed record RemoveOrderCommand(Guid TourId, Guid TransportOrderId) : ICommand<TourResponse>;
