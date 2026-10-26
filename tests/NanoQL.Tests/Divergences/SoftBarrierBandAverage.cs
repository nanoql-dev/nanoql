// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The soft barrier's definition as #2883's test states it, "the average of the single-barrier options with barriers in
/// [L, U]", computed independently of the engine: composite Simpson's rule over QuantLib's analytic barrier engine's
/// knock-ins, the barriers on the spot's far side knocked in (the vanilla), the band split at the spot and the strike,
/// so each piece's integrand is smooth. The soft-barrier divergence facts check the engine against it.
/// </summary>
internal static class SoftBarrierBandAverage
{
    /// <summary>The average knock-in over the band.</summary>
    /// <param name="knockIn">The knock-in type, down or up.</param>
    /// <param name="l">The band's lower barrier.</param>
    /// <param name="u">The band's upper barrier.</param>
    /// <param name="payoff">The payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process.</param>
    /// <param name="intervals">The Simpson intervals per piece, even.</param>
    /// <returns>The average.</returns>
    public static double KnockIn(
        BarrierType knockIn,
        double l,
        double u,
        PlainVanillaPayoff payoff,
        Exercise exercise,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int intervals = 2000)
    {
        double spot = process.X0;
        bool down = knockIn == BarrierType.DownIn;
        double vanilla = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, exercise), context, process).NPV!.Value;
        double Di(double h)
        {
            if (down ? h >= spot : h <= spot)
            {
                return vanilla;
            }

            return AnalyticBarrierEngine.Calculate(new BarrierOption(knockIn, h, 0.0, payoff, exercise), context, process).NPV!.Value;
        }

        var points = new SortedSet<double> { l, u };
        foreach (double x in new[] { spot, payoff.Strike })
        {
            if (x > l && x < u)
            {
                points.Add(x);
            }
        }

        double[] p = [.. points];
        double sum = 0.0;
        for (int k = 0; k < p.Length - 1; ++k)
        {
            double a = p[k], b = p[k + 1];
            double h = (b - a) / intervals;
            double piece = 0.0;
            for (int i = 0; i <= intervals; ++i)
            {
                double weight = (i == 0 || i == intervals) ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0);
                double x = i == intervals ? b : a + (i * h);

                // a hair inside each end, where the integrand steps at the spot
                if (i == 0)
                {
                    x = a + (1e-9 * h);
                }
                else if (i == intervals)
                {
                    x = b - (1e-9 * h);
                }

                piece += weight * Di(x);
            }

            sum += piece * h / 3.0;
        }

        return sum / (u - l);
    }
}
