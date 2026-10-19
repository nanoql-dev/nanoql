// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/trinomialtree.hpp ql/methods/lattices/trinomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Methods;

/// <summary>A recombining trinomial tree approximating a one-dimensional stochastic process.</summary>
/// <remarks>
/// <para>
/// The diffusion term of the process must be independent of the underlying variable. Each node has three descendants,
/// the middle one the node closest to the expectation of the variable. On a step shorter than 1% of the grid's longest,
/// the spacing is floored at the largest natural spacing of the grid (Clewlow and Strickland 1998), which keeps a short
/// mandatory step from widening the tree; the floored step's probabilities are the general moment-matching ones, which
/// can be slightly negative.
/// </para>
/// <para>
/// Immutable once built. A NaN or an out-of-range node offset converts to the smallest integer, as QuantLib's
/// conversion does on MSVC, so the classical regime's probability check refuses as QuantLib's does.
/// </para>
/// </remarks>
public sealed class TrinomialTree : Tree
{
    // Floor activation threshold: the dx floor (Clewlow-Strickland 1998)
    // is applied only on grid steps shorter than `kFloorThreshold * dtMax`.
    // Two orders of magnitude leaves uniform and typical non-uniform
    // grids (weekend rolls, 1-day mismatches) untouched; the floor
    // intervenes only on the pathological small-mandatory-gap case.
    private const double KFloorThreshold = 0.01;

    private readonly Branching[] branchings;
    private readonly double x0;
    private readonly double[] dx;

    /// <summary>Initializes a new instance of the <see cref="TrinomialTree"/> class.</summary>
    /// <param name="process">The process the tree approximates.</param>
    /// <param name="timeGrid">The time grid: one column per time.</param>
    /// <param name="isPositive">Whether the underlying variable must stay positive, bumping each node's middle branch up until its lower descendant is.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> or <paramref name="timeGrid"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeGrid"/> has fewer than two times.</exception>
    /// <exception cref="NanoQLException">A probability of the classical regime is negative or NaN.</exception>
    public TrinomialTree(StochasticProcess1D process, TimeGrid timeGrid, bool isPositive = false)
        : base(NotNull(timeGrid).Size)
    {
        ArgumentNullException.ThrowIfNull(process);
        TimeGrid = timeGrid;
        x0 = process.X0;

        // QuantLib's Size arithmetic wraps for an empty grid and fails allocating; int arithmetic reaches its check.
        int nTimeSteps = timeGrid.Size - 1;
        if (!(nTimeSteps > 0))
        {
            throw new ArgumentException("null time steps for trinomial tree", nameof(timeGrid));
        }

        // Preflight: capture per-step variances and dtMax in one pass.
        // dxFloor is the largest natural dx in the grid: when applied
        // on a tiny step it produces a dx no larger than what some
        // other step is already using, preventing node explosion.
        double dtMax = 0.0;
        double[] v2Cache = new double[nTimeSteps];
        double dxFloorVar = 0.0;
        for (int i = 0; i < nTimeSteps; i++)
        {
            double dtI = timeGrid.Dt(i);
            dtMax = Max(dtMax, dtI);
            double v2I = process.Variance(timeGrid[i], 0.0, dtI);
            v2Cache[i] = v2I;
            dxFloorVar = Max(dxFloorVar, v2I);
        }

        double dxFloor = Math.Sqrt(3.0 * dxFloorVar);

        dx = new double[nTimeSteps + 1];
        dx[0] = 0.0;
        branchings = new Branching[nTimeSteps];

        int jMin = 0;
        int jMax = 0;

        for (int i = 0; i < nTimeSteps; i++)
        {
            double t = timeGrid[i];
            double dt = timeGrid.Dt(i);

            // Variance must be independent of x
            double v2 = v2Cache[i];
            double v = Math.Sqrt(v2);
            double dxNatural = v * Math.Sqrt(3.0);
            double dxNext = (dt < KFloorThreshold * dtMax) ? Max(dxNatural, dxFloor) : dxNatural;
            dx[i + 1] = dxNext;

            // dxIsFloored captures whether the floor was *effective*
            // at this step (dx widened beyond its natural value),
            // not just whether the gate condition fired.
            bool dxIsFloored = dxNext > dxNatural;
            double dx2 = dxNext * dxNext;

            var branching = new Branching(jMax >= jMin ? jMax - jMin + 1 : 0);
            for (int j = jMin; j <= jMax; j++)
            {
                double x = x0 + (j * dx[i]);
                double m = process.Expectation(t, x, dt);
                int temp = MsvcConversions.ToInt32(Math.Floor(((m - x0) / dx[i + 1]) + 0.5));

                bool tempBumped = false;
                if (isPositive)
                {
                    while (x0 + ((temp - 1) * dx[i + 1]) <= 0)
                    {
                        temp++;
                        tempBumped = true;
                    }
                }

                double e = m - (x0 + (temp * dx[i + 1]));
                double e2 = e * e;

                double p1, p2, p3;
                if (dxIsFloored)
                {
                    // General moment-matching probabilities valid for
                    // any grid spacing dx, used only when the floor
                    // widened dx beyond v*sqrt(3).
                    p1 = (v2 + e2 - (e * dxNext)) / (2.0 * dx2);
                    p2 = 1.0 - ((v2 + e2) / dx2);
                    p3 = (v2 + e2 + (e * dxNext)) / (2.0 * dx2);
                }
                else
                {
                    // Classical Hull-White / Clewlow trinomial
                    // probabilities for dx = v*sqrt(3), kept in this
                    // exact form.
                    double e3 = e * Math.Sqrt(3.0);
                    p1 = (1.0 + (e2 / v2) - (e3 / v)) / 6.0;
                    p2 = (2.0 - (e2 / v2)) / 3.0;
                    p3 = (1.0 + (e2 / v2) + (e3 / v)) / 6.0;
                }

                // In the unfloored regime with naturally-rounded temp
                // the formulas above are non-negative by construction;
                // guard against future drift in this safe path.
                if (!dxIsFloored && !tempBumped)
                {
                    if (!(p1 >= 0.0 && p2 >= 0.0 && p3 >= 0.0))
                    {
                        throw new NanoQLException(
                            $"negative probability in trinomial tree (unfloored regime) at step {i}, node {j}: "
                            + $"p1={Show(p1)}, p2={Show(p2)}, p3={Show(p3)} (v={Show(v)}, dx={Show(dxNext)}, e={Show(e)})");
                    }
                }

                branching.Add(temp, p1, p2, p3);
            }

            branchings[i] = branching;

            jMin = branching.JMin;
            jMax = branching.JMax;
        }
    }

    /// <summary>Gets the tree's time grid.</summary>
    public TimeGrid TimeGrid { get; }

    /// <summary>Gets the number of branches from each node: 3.</summary>
    public override int Branches => 3;

    /// <summary>The spacing of a column's nodes: 0 for the first column, and the spacing of step i's descendants at i + 1.</summary>
    /// <param name="i">The column.</param>
    /// <returns>The spacing.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a column of the tree.</exception>
    public double Dx(int i)
    {
        CheckColumn(i, dx.Length, nameof(i));
        return dx[i];
    }

    /// <summary>The number of nodes in a column.</summary>
    /// <param name="i">The column.</param>
    /// <returns>The number of nodes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a column of the tree.</exception>
    public override int Size(int i)
    {
        CheckColumn(i, branchings.Length + 1, nameof(i));
        return i == 0 ? 1 : branchings[i - 1].Size;
    }

    /// <summary>The underlying variable's value at a node.</summary>
    /// <param name="i">The column.</param>
    /// <param name="index">The node's index in the column.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a column of the tree, or <paramref name="index"/> is negative.</exception>
    /// <remarks>As QuantLib's, it computes from the column's lowest offset and spacing for any index, past the column too.</remarks>
    public override double Underlying(int i, int index)
    {
        CheckColumn(i, branchings.Length + 1, nameof(i));
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (i == 0)
        {
            return x0;
        }

        return x0 + ((branchings[i - 1].JMin + (double)index) * dx[i]);
    }

    /// <summary>The index, in the next column, of a node's descendant along a branch.</summary>
    /// <param name="i">The column.</param>
    /// <param name="index">The node's index in the column.</param>
    /// <param name="branch">The branch, 0 to 2.</param>
    /// <returns>The descendant's index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> or <paramref name="index"/> is out of range, or <paramref name="branch"/> is negative.
    /// </exception>
    /// <remarks>As QuantLib's, it computes from the node's middle descendant for any branch, past the third too.</remarks>
    public override int Descendant(int i, int index, int branch)
    {
        CheckColumn(i, branchings.Length, nameof(i));
        Branching branching = branchings[i];
        CheckColumn(index, branching.Count, nameof(index));
        ArgumentOutOfRangeException.ThrowIfNegative(branch);
        return branching.Descendant(index, branch);
    }

    /// <summary>The probability of a node's branch.</summary>
    /// <param name="i">The column.</param>
    /// <param name="index">The node's index in the column.</param>
    /// <param name="branch">The branch, 0 to 2.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is out of range.</exception>
    public override double Probability(int i, int index, int branch)
    {
        Branching branching = BranchingAt(i, index, branch);
        return branching.Probability(index, branch);
    }

    private static TimeGrid NotNull(TimeGrid timeGrid)
    {
        ArgumentNullException.ThrowIfNull(timeGrid);
        return timeGrid;
    }

    // std::max(a, b): a < b ? b : a
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private static void CheckColumn(int i, int count, string paramName)
    {
        // QuantLib reads its vectors unchecked.
        if ((uint)i >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(paramName, i, $"index {i} out of range [0, {count})");
        }
    }

    private Branching BranchingAt(int i, int index, int branch)
    {
        CheckColumn(i, branchings.Length, nameof(i));
        Branching branching = branchings[i];
        CheckColumn(index, branching.Count, nameof(index));
        CheckColumn(branch, 3, nameof(branch));
        return branching;
    }

    // Branching scheme for a trinomial node.  Each node has three
    // descendants, with the middle branch linked to the node
    // which is closest to the expectation of the variable.
    private sealed class Branching
    {
        private readonly int[] k;
        private readonly double[][] probs;
        private int kMin = NumericLimits.QL_MAX_INTEGER;
        private int kMax = NumericLimits.QL_MIN_INTEGER;

        public Branching(int capacity)
        {
            k = new int[capacity];
            probs = [new double[capacity], new double[capacity], new double[capacity]];
        }

        public int Count { get; private set; }

        public int JMin { get; private set; } = NumericLimits.QL_MAX_INTEGER;

        public int JMax { get; private set; } = NumericLimits.QL_MIN_INTEGER;

        public int Size => JMax - JMin + 1;

        public int Descendant(int index, int branch) => k[index] - JMin - 1 + branch;

        public double Probability(int index, int branch) => probs[branch][index];

        public void Add(int kk, double p1, double p2, double p3)
        {
            // store
            k[Count] = kk;
            probs[0][Count] = p1;
            probs[1][Count] = p2;
            probs[2][Count] = p3;
            Count++;

            // maintain invariants
            kMin = Math.Min(kMin, kk);
            JMin = kMin - 1;
            kMax = Math.Max(kMax, kk);
            JMax = kMax + 1;
        }
    }
}
