// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.hpp ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Methods;

/// <summary>
/// The one-dimensional mesher of the Heston model's variance: points averaged over the variance's non-central
/// chi-squared quantiles at several times up to maturity, v₀ among them, with an estimate of the average volatility
/// that sizes the equity's mesher.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. Where the quantile grid fails, its default mesh: evenly spaced over θ and v₀ ± 4σ√(θ/2κ).
/// </para>
/// <para>
/// QuantLib's master since 1.44 (#2908's mesher part and #2940, taken as fixes) falls back to the default mesh also
/// where a normal approximation of the quantiles shows the distribution too narrow for the grid's minimum steps, where
/// the quantiles can't hold v₀ (checked before and after building the grid), and where the grid is degenerate, its
/// probabilities repeating; and it skips the quantiles beyond 1 − ε, which give the same grid. 1.44's grid holds no v₀
/// where the variance falls fast (v₀ 0.25, θ 0.0025, κ 10), so its engines throw, and at small σ it builds degenerate
/// grids; elsewhere the two are the same, bit for bit (<c>Divergences/FdmHestonVarianceMesherFallbackTests.cs</c>).
/// </para>
/// <para>
/// The volatility estimate scales by a skew factor, max(1, σ_m/κ)^1.5 in QuantLib, σ_m the mixing factor times σ, which
/// at small κ widens the equity's mesher until its prices leave the analytic engine's: +30% at σ 1.0 and κ 0.1, +638%
/// at κ 0.02, refining the grid not bringing them back. Here the factor is bounded at σ_m/κ = 2, min(2, max(1,
/// σ_m/κ))^1.5, so QuantLib's bits stand wherever σ_m/κ ≤ 2, every QuantLib case among them
/// (<c>Divergences/FdHestonSkewFactorTests.cs</c>).
/// </para>
/// </remarks>
public sealed class FdmHestonVarianceMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="FdmHestonVarianceMesher"/> class.</summary>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="tAvgSteps">The number of times the quantiles are averaged over.</param>
    /// <param name="epsilon">The probability left above the last quantile.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> or <paramref name="tAvgSteps"/> is
    /// negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="size"/> is 0 or 1.</exception>
    /// <exception cref="NanoQLException">The volatility estimate's integration fails.</exception>
    public FdmHestonVarianceMesher(
        int size, HestonProcess process, double maturity, int tAvgSteps = 10, double epsilon = 0.0001, double mixingFactor = 1.0)
        : base(size)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(tAvgSteps);
        RequireTwoPoints(size, nameof(size));

        double[] vGrid = new double[size], pGrid = new double[size];
        double mixedSigma = process.Sigma * mixingFactor;
        double df = 4 * process.Theta * process.Kappa / Squared(mixedSigma);
        if (!TryQuantileGrid(size, process, maturity, tAvgSteps, epsilon, mixedSigma, df, vGrid, pGrid))
        {
            // use default mesh
            double vol = mixedSigma * Math.Sqrt(process.Theta / (2 * process.Kappa));

            double mean = process.Theta;
            double upperBound = Max(process.V0 + (4 * vol), mean + (4 * vol));
            double lowerBound = Max(0.0, Min(process.V0 - (4 * vol), mean - (4 * vol)));

            for (int i = 0; i < size; ++i)
            {
                pGrid[i] = i / (size - 1.0);
                vGrid[i] = lowerBound + (i * (upperBound - lowerBound) / (size - 1.0));
            }
        }

        // the skew factor bounded at sigma_m/kappa = 2, QuantLib's below
        double skewHint = (process.Kappa != 0.0) ? Min(2.0, Max(1.0, mixedSigma / process.Kappa)) : 1.0;

        Array.Sort(pGrid);
        var variance = new LinearInterpolation(pGrid, vGrid);
        VolaEstimate = new GaussLobattoIntegral(100000, 1e-4).Integrate(
            x => Math.Sqrt(variance.Evaluate(x, true)), pGrid[0], pGrid[^1]) * Math.Pow(skewHint, 1.5);

        double v0 = process.V0;
        for (int i = 1; i < vGrid.Length; ++i)
        {
            if (vGrid[i - 1] <= v0 && vGrid[i] >= v0)
            {
                if (Math.Abs(vGrid[i - 1] - v0) < Math.Abs(vGrid[i] - v0))
                {
                    vGrid[i - 1] = v0;
                }
                else
                {
                    vGrid[i] = v0;
                }
            }
        }

        vGrid.CopyTo(locations, 0);

        for (int i = 0; i < size - 1; ++i)
        {
            dminus[i + 1] = dplus[i] = vGrid[i + 1] - vGrid[i];
        }

        dplus[^1] = dminus[0] = NullReal;
    }

    /// <summary>Gets the estimate of the variance's average volatility, scaled by the bounded skew factor.</summary>
    public double VolaEstimate { get; }

    private static double Squared(double x) => x * x;

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    // QuantLib's try block: the quantile grid, or false where it fails or a check of master's (#2908, #2940) sends it
    // to the default mesh, QuantLib's catch.
    private static bool TryQuantileGrid(
        int size,
        HestonProcess process,
        double maturity,
        int tAvgSteps,
        double epsilon,
        double mixedSigma,
        double df,
        double[] vGrid,
        double[] pGrid)
    {
        try
        {
            var grid = new List<(double V, double P)>(size * tAvgSteps);

            const double QMin = 0.0; // v_min = 0.0;
            double[] ncp = new double[tAvgSteps + 1], k = new double[tAvgSteps + 1], qMax = new double[tAvgSteps + 1];
            double z = new InverseCumulativeNormal().Evaluate(1 - epsilon);
            bool narrow = true;
            for (int l = 1; l <= tAvgSteps; ++l)
            {
                double t = maturity * l / tAvgSteps;
                ncp[l] = 4 * process.Kappa * Math.Exp(-process.Kappa * t)
                    / (Squared(mixedSigma) * (1 - Math.Exp(-process.Kappa * t))) * process.V0;
                k[l] = Squared(mixedSigma) * (1 - Math.Exp(-process.Kappa * t)) / (4 * process.Kappa);

                // normal approximation of the quantiles, compared below
                // with the (size-1) minimum steps of the grid
                double sd = Math.Sqrt(2 * (df + (2 * ncp[l])));
                double q = Max(process.V0, k[l] * (df + ncp[l] + (z * sd)));
                if (2 * z * sd * k[l] >= (size - 1) * (q - QMin) / (50 * size))
                {
                    narrow = false;
                }
            }

            // with a small vol of vol, the distribution can be so narrow
            // that the minimum steps take most points beyond its quantiles
            // and the grid is degenerate; use the default mesh
            if (narrow)
            {
                return false;
            }

            bool containsV0 = false;
            for (int l = 1; l <= tAvgSteps; ++l)
            {
                double q = k[l] * new InverseNonCentralCumulativeChiSquareDistribution(df, ncp[l], 100, 1e-8)
                    .Evaluate(1 - epsilon);
                qMax[l] = Max(process.V0, q);

                // the points built below for this l are at most
                // q + (size-1)*minVStep
                if (q + ((size - 1) * (qMax[l] - QMin) / (50 * size)) >= process.V0)
                {
                    containsV0 = true;
                }
            }

            // with a small vol of vol, the variance can drift away from
            // v0 and leave it outside the quantiles; use the default mesh
            if (!containsV0)
            {
                return false;
            }

            for (int l = 1; l <= tAvgSteps; ++l)
            {
                double minVStep = (qMax[l] - QMin) / (50 * size);
                double ps, p = 0.0;

                double vTmp = QMin;
                grid.Add((QMin, epsilon));

                var inverse = new InverseNonCentralCumulativeChiSquareDistribution(df, ncp[l], 100, 1e-8);
                var cumulative = new NonCentralCumulativeChiSquareDistribution(df, ncp[l]);
                for (int i = 1; i < size; ++i)
                {
                    double vx = vTmp + minVStep;

                    // beyond the 1-epsilon quantile, p can only decrease
                    // and its quantile is below vTmp; no need to compute it
                    if (p < 1 - epsilon)
                    {
                        ps = (1 - epsilon - p) / (size - i);
                        p += ps;
                        double tmp = k[l] * inverse.Evaluate(p);

                        vx = Max(vx, tmp);
                    }

                    p = cumulative.Evaluate(vx / k[l]);
                    vTmp = vx;
                    grid.Add((vx, p));
                }
            }

            // std::multiset's order: the pairs sorted lexicographically
            grid.Sort(static (a, b) => a.V < b.V ? -1 : b.V < a.V ? 1 : a.P < b.P ? -1 : b.P < a.P ? 1 : 0);

            int n = grid.Count;
            for (int i = 0; i < size; ++i)
            {
                int b = (int)((long)i * n / size);
                int e = (int)((long)(i + 1) * n / size);
                for (int j = b; j < e; ++j)
                {
                    vGrid[i] += grid[j].V / (e - b);
                    pGrid[i] += grid[j].P / (e - b);
                }
            }

            // the check above can't rule out all such cases
            if (!(vGrid[^1] >= process.V0))
            {
                return false;
            }

            // with a very narrow distribution, most points are beyond
            // the 1-epsilon quantile and have the same probability
            double[] sorted = [.. pGrid];
            Array.Sort(sorted);
            for (int i = 1; i < sorted.Length; ++i)
            {
                if (sorted[i] == sorted[i - 1])
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            return false;
        }
    }
}
