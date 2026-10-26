// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Bachelier calculator on payoffs other than the plain vanilla. QuantLib's visitor sets alpha, beta and x for
/// cash-or-nothing, asset-or-nothing and gap payoffs, which its value and Greeks never read
/// (<c>bacheliercalculator.cpp:137-185</c>), so each prices 3.03, the vanilla's, at K 100, F 100, σ√T 8, where the
/// payoffs are worth 4.75 (cash 10), 50.53 and 7.78 (second strike 90). nanoql values each by Bachelier's closed form
/// and takes the Greeks as its derivatives (the S-exo plan's ER5 (xxvii)). The checks: the closed forms under a normal
/// forward, F_T ~ N(F, σ²), and their bumps (forward, deviation and strike by 1e-5 each way, the gamma's by 1e-3).
/// </summary>
public class BachelierPayoffTests
{
    private const double F = 100.0, Sd = 8.0, D = 0.95, T = 2.0;

    [Fact]
    public void ACashOrNothingPayoffIsWorthTheDiscountedProbability()
    {
        // D·X·N(±d)
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double k in new[] { 95.0, 100.0, 108.0 })
            {
                double Closed(double f, double sd, double strike) =>
                    D * 10.0 * Phi((double)type * (f - strike) / sd);
                Check(new CashOrNothingPayoff(type, k, 10.0), k, Closed);
            }
        }
    }

    [Fact]
    public void AnAssetOrNothingPayoffIsWorthTheDiscountedTruncatedForward()
    {
        // D·(F·N(d) + σ·n(d)) for the call, D·(F·N(−d) − σ·n(d)) for the put
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double k in new[] { 95.0, 100.0, 108.0 })
            {
                double Closed(double f, double sd, double strike)
                {
                    double d = (f - strike) / sd;
                    return D * ((f * Phi((double)type * d)) + ((double)type * sd * Density(d)));
                }

                Check(new AssetOrNothingPayoff(type, k), k, Closed);
            }
        }
    }

    [Fact]
    public void AGapPayoffIsWorthItsBachelierValue()
    {
        // D·((F − K₂)·N(d) + σ·n(d)) for the call, D·((K₂ − F)·N(−d) + σ·n(d)) for the put, d on the first strike
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double k in new[] { 95.0, 100.0, 108.0 })
            {
                double Closed(double f, double sd, double strike)
                {
                    double d = (f - strike) / sd;
                    return D * (((double)type * (f - 90.0) * Phi((double)type * d)) + (sd * Density(d)));
                }

                Check(new GapPayoff(type, k, 90.0), k, Closed);
            }
        }
    }

    // The value against the closed form, and the forward Greeks, the vega and the strike sensitivity against its bumps.
    private static void Check(StrikedTypePayoff payoff, double strike, Func<double, double, double, double> closed)
    {
        const double h = 1e-5;
        var calc = new BachelierCalculator(payoff, F, Sd, D);
        double value = closed(F, Sd, strike);
        Assert.Equal(value, calc.Value, 1e-12 * Math.Max(1.0, value));
        Assert.Equal((closed(F + h, Sd, strike) - closed(F - h, Sd, strike)) / (2.0 * h), calc.DeltaForward, 1e-7);
        const double hGamma = 1e-3;
        Assert.Equal(
            (closed(F + hGamma, Sd, strike) - (2.0 * value) + closed(F - hGamma, Sd, strike)) / (hGamma * hGamma),
            calc.GammaForward,
            1e-6);
        Assert.Equal(
            Math.Sqrt(T) * (closed(F, Sd + h, strike) - closed(F, Sd - h, strike)) / (2.0 * h), calc.Vega(T), 1e-6);
        Assert.Equal(
            (closed(F, Sd, strike + h) - closed(F, Sd, strike - h)) / (2.0 * h), calc.StrikeSensitivity, 1e-7);
    }

    private static double Phi(double x) => new CumulativeNormalDistribution().Evaluate(x);

    private static double Density(double x) => new NormalDistribution().Evaluate(x);
}
