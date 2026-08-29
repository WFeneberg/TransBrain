namespace TransBrain.Domain.Tours;

/// <summary>
/// One call on a tour. Two of these exist per assigned order — a <see cref="StopType.Pickup"/>
/// and a <see cref="StopType.Delivery"/> — and the pickup always carries the lower sequence.
/// </summary>
/// <remarks>
/// Created only by <see cref="Tour"/>, which is what keeps the pickup-before-delivery and
/// contiguous-sequence invariants true: nothing outside the aggregate can add a stop.
/// </remarks>
public sealed record TourStop
{
    private TourStop(int sequence, Guid transportOrderId, StopType stopType)
    {
        Sequence = sequence;
        TransportOrderId = transportOrderId;
        StopType = stopType;
    }

    public int Sequence { get; }

    public Guid TransportOrderId { get; }

    public StopType StopType { get; }

    internal static TourStop Create(int sequence, Guid transportOrderId, StopType stopType) =>
        new(sequence, transportOrderId, stopType);

    internal TourStop WithSequence(int sequence) => new(sequence, TransportOrderId, StopType);
}
