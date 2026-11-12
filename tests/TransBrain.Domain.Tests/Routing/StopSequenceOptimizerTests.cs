using AwesomeAssertions;
using TransBrain.Domain.Routing;

namespace TransBrain.Domain.Tests.Routing;

public class StopSequenceOptimizerTests
{
    private static GeoPoint At(double latitude, double longitude) =>
        GeoPoint.Create(latitude, longitude).Value;

    private static double LengthOf(IReadOnlyList<SequencedStop> stops, IReadOnlyList<int> order)
    {
        double total = 0;
        for (int i = 1; i < order.Count; i++)
        {
            total += stops[order[i - 1]].Point.StraightLineMetersTo(stops[order[i]].Point);
        }

        return total;
    }

    [Fact]
    public void Optimize_FewerThanThreeStops_ReturnsIdentity()
    {
        Guid order = Guid.NewGuid();
        List<SequencedStop> stops =
        [
            new(order, true, At(48.0, 11.0)),
            new(order, false, At(52.0, 13.0)),
        ];

        StopSequenceOptimizer.Optimize(stops).Should().Equal(0, 1);
    }

    [Fact]
    public void Optimize_ZigZagBetweenTwoCities_GroupsStopsByCity()
    {
        // P1 (Munich) D1 (Berlin) P2 (Munich) D2 (Berlin): as planned, the lorry crosses the
        // country twice. Picking up both in Munich first halves the distance.
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        List<SequencedStop> stops =
        [
            new(first, true, At(48.137, 11.575)),
            new(first, false, At(52.520, 13.405)),
            new(second, true, At(48.140, 11.580)),
            new(second, false, At(52.525, 13.410)),
        ];

        IReadOnlyList<int> optimized = StopSequenceOptimizer.Optimize(stops);

        LengthOf(stops, optimized).Should().BeLessThan(LengthOf(stops, [0, 1, 2, 3]) / 1.9);
        // Both pickups (Munich) come before both deliveries (Berlin).
        optimized.Take(2).Should().BeEquivalentTo([0, 2]);
        optimized.Skip(2).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void Optimize_EveryAnswer_KeepsDeliveriesBehindTheirPickups()
    {
        // Three orders whose pickups and deliveries interleave geographically, so the shortest
        // unconstrained path would deliver before picking up.
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        Guid c = Guid.NewGuid();
        List<SequencedStop> stops =
        [
            new(a, true, At(50.0, 8.0)),
            new(a, false, At(50.0, 8.9)),
            new(b, true, At(50.0, 8.5)),
            new(b, false, At(50.0, 8.1)),
            new(c, true, At(50.0, 8.7)),
            new(c, false, At(50.0, 8.3)),
        ];

        List<int> optimized = [.. StopSequenceOptimizer.Optimize(stops)];

        optimized.Should().BeEquivalentTo(Enumerable.Range(0, stops.Count));
        optimized.IndexOf(0).Should().BeLessThan(optimized.IndexOf(1));
        optimized.IndexOf(2).Should().BeLessThan(optimized.IndexOf(3));
        optimized.IndexOf(4).Should().BeLessThan(optimized.IndexOf(5));
    }

    [Fact]
    public void Optimize_AlreadyShortestOrder_ReturnsIdentity()
    {
        // Munich -> Nuremberg -> Leipzig -> Berlin is already a straight south-to-north run.
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        List<SequencedStop> stops =
        [
            new(first, true, At(48.137, 11.575)),
            new(first, false, At(49.454, 11.077)),
            new(second, true, At(51.340, 12.375)),
            new(second, false, At(52.520, 13.405)),
        ];

        StopSequenceOptimizer.Optimize(stops).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void Optimize_SameInput_AnswersTheSameOrder()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        List<SequencedStop> stops =
        [
            new(first, true, At(48.137, 11.575)),
            new(first, false, At(52.520, 13.405)),
            new(second, true, At(48.140, 11.580)),
            new(second, false, At(52.525, 13.410)),
        ];

        StopSequenceOptimizer.Optimize(stops).Should().Equal(StopSequenceOptimizer.Optimize(stops));
    }
}
