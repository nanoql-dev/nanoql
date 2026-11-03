// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.hpp ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The variance mesher of a Heston model with a leverage function: <see cref="FdmHestonVarianceMesher"/>'s points,
/// its volatility estimate scaled by the leverage's average over the maturity.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. Without a leverage function it is the variance mesher. The skew factor is the variance mesher's,
/// bounded at σ_m/κ = 2, and its fallbacks master's.
/// </para>
/// <para>
/// QuantLib reads the curves and the leverage function at the risk-free curve's times
/// (<c>fdmhestonvariancemesher.cpp:176-201</c>). Here the times are the finite-difference Heston engine's clock's
/// (<see cref="FdmHestonOp.Clock"/>), from the earliest reference date of the structures it reads, and each structure
/// is read at the instants they denote, on its own clock, accruing nothing before its own reference date; the leverage,
/// a multiplier of the Heston volatility, at its instant. Where every structure shares the risk-free curve's day
/// counter and reference date, QuantLib's reads.
/// </para>
/// </remarks>
public sealed class FdmHestonLocalVolatilityVarianceMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="FdmHestonLocalVolatilityVarianceMesher"/> class.</summary>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <param name="maturity">The maturity, a time on <see cref="FdmHestonOp.Clock"/> of the process and the leverage
    /// function.</param>
    /// <param name="tAvgSteps">The number of times the quantiles and the leverage are averaged over.</param>
    /// <param name="epsilon">The probability left above the last quantile, and below and above the leverage's
    /// average.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> or <paramref name="tAvgSteps"/> is
    /// negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="size"/> is 0 or 1.</exception>
    /// <exception cref="NanoQLException">An integration fails, or a curve or the leverage function can't be
    /// read.</exception>
    public FdmHestonLocalVolatilityVarianceMesher(
        int size,
        HestonProcess process,
        LocalVolTermStructure? leverageFct,
        double maturity,
        int tAvgSteps = 10,
        double epsilon = 0.0001,
        double mixingFactor = 1.0)
        : this(size, process, leverageFct, maturity, tAvgSteps, epsilon, mixingFactor, FdmHestonOp.Clock(process, null, leverageFct))
    {
    }

    // The finite-difference Heston engine's form: its clock, which may hold the quanto helper's structures too.
    internal FdmHestonLocalVolatilityVarianceMesher(
        int size,
        HestonProcess process,
        LocalVolTermStructure? leverageFct,
        double maturity,
        int tAvgSteps,
        double epsilon,
        double mixingFactor,
        TermStructure clock)
        : base(size)
    {
        var mesher = new FdmHestonVarianceMesher(size, process, maturity, tAvgSteps, epsilon, mixingFactor);

        for (int i = 0; i < size; ++i)
        {
            dplus[i] = mesher.Dplus(i);
            dminus[i] = mesher.Dminus(i);
            locations[i] = mesher.Location(i);
        }

        VolaEstimate = mesher.VolaEstimate;

        if (leverageFct is not null)
        {
            // boost::accumulators' mean: the sum, in order, over the count
            double sum = 0.0;
            int count = 0;

            double s0 = process.S0;

            sum += leverageFct.LocalVol(LeverageTime(leverageFct, clock, 0.0), s0, true);
            ++count;

            YieldTermStructure rTS = process.RiskFreeRate;
            YieldTermStructure qTS = process.DividendYield;

            for (int l = 1; l <= tAvgSteps; ++l)
            {
                double t = maturity * l / tAvgSteps;
                double vol = VolaEstimate * (sum / count);

                double fwd = s0 * qTS.DiscountAt(clock, t) / rTS.DiscountAt(clock, t);

                const int SAvgSteps = 50;

                double[] u = new double[SAvgSteps], sig = new double[SAvgSteps];

                double lvTime = LeverageTime(leverageFct, clock, t);
                for (int i = 0; i < SAvgSteps; ++i)
                {
                    u[i] = epsilon + ((1 - (2 * epsilon)) / (SAvgSteps - 1) * i);
                    double x = new InverseCumulativeNormal().Evaluate(u[i]);

                    double gf = x * vol * Math.Sqrt(t);
                    double f = fwd * Math.Exp(gf);

                    double lv = leverageFct.LocalVol(lvTime, f, true);
                    sig[i] = lv * lv;
                }

                var interpolation = new LinearInterpolation(u, sig);
                double leverageAvg = new GaussLobattoIntegral(10000, 1e-4).Integrate(
                    x => Math.Sqrt(interpolation.Evaluate(x, true)), u[0], u[^1])
                    / (1 - (2 * epsilon));

                sum += leverageAvg;
                ++count;
            }

            VolaEstimate *= sum / count;
        }
    }

    /// <summary>Gets the estimate of the variance's average volatility, scaled by the bounded skew factor and, with a
    /// leverage function, by the leverage's average.</summary>
    public double VolaEstimate { get; }

    // The leverage function's own time at the instant a time on the engine's clock denotes: the time itself where the
    // two share their times, QuantLib's read.
    internal static double LeverageTime(LocalVolTermStructure leverageFct, TermStructure clock, double t) =>
        leverageFct.SharesTimes(clock) ? t : leverageFct.AccrualTime(clock, t);
}
