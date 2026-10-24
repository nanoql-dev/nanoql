// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CEV formula's small tails (the S-van plan's VR4 (xxxiv)). <c>CEVCalculator::value</c> takes a tail as
/// 1 − cdf (or Γ-ratio − cdf, <c>analyticcevengine.cpp:50-80</c>), which loses most of its digits where the tail is
/// small: a 36-day call at K 150 comes out about 11% low. nanoql takes Boost's complement where QuantLib's difference is
/// below 1e-3, and QuantLib's arithmetic and bits elsewhere. The check: Hagan's ν = 0 Black (QuantLib's
/// <c>unsafeSabrLogNormalVolatility</c>), within 2% of the CEV value at |β − 1| = 1e-2 and 36 days, and QuantLib's FD CEV
/// engine (200 × 2,000), 5.70e-11 and 6.83e-11 [executed on the wheel and C++: <c>scratchpad\c3\oracle_c3.py</c>, the
/// planner's VP57; f0 100, σ 20%, r 3% on Actual/365 (Fixed), from 15 October 2026].
/// </summary>
public class CevTailTests
{
    private const double F0 = 100.0;

    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Discount = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Theory]
    [InlineData(0.99, 4.993087249659152e-11, 5.604777998884425e-11)]
    [InlineData(1.01, 6.091343550960677e-11, 6.714298013083215e-11)]
    public void ASmallTailTakesTheComplement(double beta, double quantLib, double hagan)
    {
        // the 36-day call at K 150: QuantLib's 1 − cdf about 11% below Hagan's Black, nanoql within 2% of it
        double value = Price(OptionType.Call, 150.0, 36, beta);
        Assert.True(Math.Abs(value - hagan) <= 0.02 * hagan, $"{value} against Hagan's {hagan}");
        Assert.True(Math.Abs(quantLib - hagan) > 0.09 * hagan);
    }

    [Theory]
    [InlineData(0.99, OptionType.Call, 150.0, 365, 0.18434851096928895)]
    [InlineData(0.99, OptionType.Call, 120.0, 365, 2.078763014954287)]
    [InlineData(0.99, OptionType.Call, 100.0, 36, 2.4979771699782907)]
    [InlineData(1.01, OptionType.Call, 150.0, 365, 0.18924920265466808)]
    [InlineData(1.01, OptionType.Call, 100.0, 36, 2.4979771699780215)]
    public void AboveTheGuardQuantLibsBitsStand(double beta, OptionType type, double strike, int days, double quantLib) =>
        Assert.Equal(quantLib, Price(type, strike, days, beta));

    private static double Price(OptionType type, double strike, int days, double beta) =>
        AnalyticCEVEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + days)),
            Context,
            F0,
            0.2 * Math.Pow(F0, 1 - beta),
            beta,
            Discount).NPV!.Value;
}
