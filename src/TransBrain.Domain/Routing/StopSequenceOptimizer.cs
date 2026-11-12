namespace TransBrain.Domain.Routing;

/// <summary>
/// One stop of a route to be sequenced. Stops sharing a <paramref name="GroupId"/> belong to the
/// same transport order; a group's delivery must never be visited before its pickup.
/// </summary>
public sealed record SequencedStop(Guid GroupId, bool IsPickup, GeoPoint Point);

/// <summary>
/// Orders route stops so the total travelled distance shrinks, while every delivery stays behind
/// its own pickup.
/// </summary>
/// <remarks>
/// Pickup-and-delivery sequencing is NP-hard, so this is a heuristic, not an exact solver:
/// greedy nearest-neighbour construction from every feasible start, then precedence-respecting
/// 2-opt improvement. Distances are air-line (<see cref="GeoPoint.StraightLineMetersTo"/>) —
/// crossing-free by air is crossing-free enough for stop ordering, and asking the road routing
/// service for an n² distance matrix on every click would be slow and rate-limited. A tour has
/// no depot, so the path is open: any pickup may start it and any stop may end it.
/// </remarks>
public static class StopSequenceOptimizer
{
    // Below one meter of saving, a reorder is churn, not an optimization: persisted sequences
    // would change although no dispatcher could tell the difference.
    private const double MinimumImprovementMeters = 1.0;

    private const int MaxImprovementPasses = 100;

    /// <summary>
    /// Answers the indices of <paramref name="stops"/> in optimized visit order. Answers the
    /// identity order whenever no strictly shorter valid order is found, so an unchanged answer
    /// is safe to treat as "nothing to do".
    /// </summary>
    public static IReadOnlyList<int> Optimize(IReadOnlyList<SequencedStop> stops)
    {
        int n = stops.Count;
        int[] identity = [.. Enumerable.Range(0, n)];
        if (n <= 2)
        {
            return identity;
        }

        double[,] distance = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double meters = stops[i].Point.StraightLineMetersTo(stops[j].Point);
                distance[i, j] = meters;
                distance[j, i] = meters;
            }
        }

        // For each stop, the index that must already be visited before it may be chosen - the
        // group's pickup for a delivery, nothing (-1) for a pickup.
        int[] prerequisiteOf = Prerequisites(stops);

        int[]? best = null;
        double bestLength = double.MaxValue;
        for (int start = 0; start < n; start++)
        {
            if (prerequisiteOf[start] >= 0)
            {
                continue;
            }

            int[] candidate = NearestNeighborFrom(start, distance, prerequisiteOf);
            double length = Length(candidate, distance);
            if (length < bestLength)
            {
                best = candidate;
                bestLength = length;
            }
        }

        if (best is null)
        {
            // No feasible start exists only when every stop has a prerequisite - impossible for
            // tour stops, but a caller handing in such a list still deserves a harmless answer.
            return identity;
        }

        TwoOptImprove(best, distance, prerequisiteOf);

        return Length(best, distance) + MinimumImprovementMeters < Length(identity, distance)
            ? best
            : identity;
    }

    private static int[] Prerequisites(IReadOnlyList<SequencedStop> stops)
    {
        Dictionary<Guid, int> pickupIndexByGroup = [];
        for (int i = 0; i < stops.Count; i++)
        {
            if (stops[i].IsPickup)
            {
                pickupIndexByGroup[stops[i].GroupId] = i;
            }
        }

        int[] prerequisiteOf = new int[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            prerequisiteOf[i] = !stops[i].IsPickup
                && pickupIndexByGroup.TryGetValue(stops[i].GroupId, out int pickup)
                    ? pickup
                    : -1;
        }

        return prerequisiteOf;
    }

    private static int[] NearestNeighborFrom(int start, double[,] distance, int[] prerequisiteOf)
    {
        int n = prerequisiteOf.Length;
        int[] path = new int[n];
        bool[] visited = new bool[n];
        path[0] = start;
        visited[start] = true;

        for (int step = 1; step < n; step++)
        {
            int current = path[step - 1];
            int next = -1;
            double nearest = double.MaxValue;
            for (int candidate = 0; candidate < n; candidate++)
            {
                bool feasible = !visited[candidate]
                    && (prerequisiteOf[candidate] < 0 || visited[prerequisiteOf[candidate]]);
                // Strictly-less keeps ties on the lowest index, which keeps the result
                // deterministic across runs.
                if (feasible && distance[current, candidate] < nearest)
                {
                    next = candidate;
                    nearest = distance[current, candidate];
                }
            }

            path[step] = next;
            visited[next] = true;
        }

        return path;
    }

    private static void TwoOptImprove(int[] path, double[,] distance, int[] prerequisiteOf)
    {
        int n = path.Length;
        for (int pass = 0; pass < MaxImprovementPasses; pass++)
        {
            bool improved = false;
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double saving = ReversalSaving(path, distance, i, j);
                    if (saving <= MinimumImprovementMeters)
                    {
                        continue;
                    }

                    Array.Reverse(path, i, j - i + 1);
                    if (RespectsPrecedence(path, prerequisiteOf))
                    {
                        improved = true;
                    }
                    else
                    {
                        Array.Reverse(path, i, j - i + 1);
                    }
                }
            }

            if (!improved)
            {
                return;
            }
        }
    }

    /// <summary>How many meters reversing path[i..j] would save. Open path: no wrap-around edge.</summary>
    private static double ReversalSaving(int[] path, double[,] distance, int i, int j)
    {
        double before = 0;
        double after = 0;
        if (i > 0)
        {
            before += distance[path[i - 1], path[i]];
            after += distance[path[i - 1], path[j]];
        }

        if (j < path.Length - 1)
        {
            before += distance[path[j], path[j + 1]];
            after += distance[path[i], path[j + 1]];
        }

        return before - after;
    }

    private static bool RespectsPrecedence(int[] path, int[] prerequisiteOf)
    {
        int[] positionOf = new int[path.Length];
        for (int position = 0; position < path.Length; position++)
        {
            positionOf[path[position]] = position;
        }

        for (int stop = 0; stop < path.Length; stop++)
        {
            if (prerequisiteOf[stop] >= 0 && positionOf[prerequisiteOf[stop]] > positionOf[stop])
            {
                return false;
            }
        }

        return true;
    }

    private static double Length(int[] path, double[,] distance)
    {
        double total = 0;
        for (int i = 1; i < path.Length; i++)
        {
            total += distance[path[i - 1], path[i]];
        }

        return total;
    }
}
