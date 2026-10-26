// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The writer-extensible engine on term structures. QuantLib evaluates Longstaff's formula on one zero rate, dividend
/// yield and volatility, read at the first exercise, for both periods (<c>analyticwriterextensibleoptionengine.cpp:61-87</c>),
/// so on a rising market the call is −2.4% off; nanoql reads the variances to each date, the correlation from their
/// ratio and the discounts by date (the S-exo plan's ER4). The check is the option's exact value at the first exercise,
/// the vanilla's payoff in the money and the extended option's Black value out of it, over the lognormal spot, within
/// 1e-9; on a flat market, QuantLib's values within its last bits.
/// </summary>
public class WriterExtensibleTermStructureTests
{
    private static readonly Date First = Today + 180, Second = Today + 270;

    [Fact]
    public void ARisingMarketMeetsTheExactValue()
    {
        Check(Rising(100.0));
    }

    [Fact]
    public void AFallingMarketMeetsTheExactValue()
    {
        Check(Falling(100.0));
    }

    [Fact]
    public void AFlatMarketIsQuantLibs()
    {
        // QuantLib's engine on the flat market (r 3%, q 0, σ 25%)
        GeneralizedBlackScholesProcess p = Flat(100.0);
        Assert.Equal(9.128267834343925, Writer(OptionType.Call, 100.0, 95.0, p), 1e-13 * 9.128267834343925);
        Assert.Equal(7.556874567628722, Writer(OptionType.Put, 100.0, 105.0, p), 1e-13 * 7.556874567628722);
    }

    private static void Check(GeneralizedBlackScholesProcess p)
    {
        foreach ((OptionType type, double x1, double x2) in new[] { (OptionType.Call, 100.0, 95.0), (OptionType.Put, 100.0, 105.0) })
        {
            // in the money at the first exercise, the vanilla pays; out of it, the option is extended
            double Payoff(double s) =>
                type == OptionType.Call
                    ? s >= x1 ? s - x1 : ForwardBlack(p, type, x2, First, Second, s)
                    : s <= x1 ? x1 - s : ForwardBlack(p, type, x2, First, Second, s);
            double exact = Exact(p, First, Payoff, x1);
            Assert.Equal(exact, Writer(type, x1, x2, p), 1e-9 * exact);
        }
    }

    private static double Writer(OptionType type, double x1, double x2, GeneralizedBlackScholesProcess p) =>
        AnalyticWriterExtensibleOptionEngine.Calculate(
            new WriterExtensibleOption(
                new PlainVanillaPayoff(type, x1), new EuropeanExercise(First), new PlainVanillaPayoff(type, x2),
                new EuropeanExercise(Second)),
            Context,
            p).NPV!.Value;
}
