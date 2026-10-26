// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The holder-extensible engine on term structures. QuantLib evaluates Longstaff's formula on one zero rate, dividend
/// yield and volatility, read at the first maturity, for both periods
/// (<c>analyticholderextensibleoptionengine.cpp:279-297</c>), so on a rising market the call is −3.5% off; nanoql reads
/// the variances to each date, the correlation from their ratio and the discounts by date, its critical prices solving
/// the forward Black values from the first maturity, on West's bivariate normal (the S-exo plan's ER4, ER7, with
/// #2892's fixes). The check is the option's exact value at the first maturity, the larger of the vanilla's payoff, the
/// extended option less the premium and nothing, over the lognormal spot, within the critical prices' accuracy, 1e-4
/// (QuantLib's Newton loops stop at 0.001); on a flat market, QuantLib's call within Drezner's accuracy and nearer the
/// exact value.
/// </summary>
public class HolderExtensibleTermStructureTests
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
    public void AFlatMarketIsQuantLibsWithinDreznersAccuracy()
    {
        GeneralizedBlackScholesProcess p = Flat(100.0);
        Check(p);

        // QuantLib's engine on the flat market (r 3%, q 0, σ 25%), its bivariate normal Drezner's
        const double quantLib = 7.972288918871404;
        double calculated = Holder(OptionType.Call, 100.0, 105.0, p);
        Assert.Equal(quantLib, calculated, 6e-7 * (100.0 + 100.0 + 105.0 + 1.0));
        double exact = ExactValue(p, OptionType.Call, 100.0, 105.0);
        Assert.True(Math.Abs(calculated - exact) <= Math.Abs(quantLib - exact));
    }

    private static void Check(GeneralizedBlackScholesProcess p)
    {
        foreach ((OptionType type, double x1, double x2) in new[] { (OptionType.Call, 100.0, 105.0), (OptionType.Put, 100.0, 95.0) })
        {
            double exact = ExactValue(p, type, x1, x2);
            Assert.Equal(exact, Holder(type, x1, x2, p), 1e-4 * exact);
        }
    }

    private static double ExactValue(GeneralizedBlackScholesProcess p, OptionType type, double x1, double x2)
    {
        double Vanilla(double s) => Math.Max((double)type * (s - x1), 0.0);
        double Extended(double s) => ForwardBlack(p, type, x2, First, Second, s) - 1.0;
        double i1 = Kink(Extended, 1e-3, 1e4);
        double i2 = Kink(s => Extended(s) - Vanilla(s), 1e-3, 1e4);
        return Exact(p, First, s => Math.Max(Math.Max(Vanilla(s), Extended(s)), 0.0), x1, i1, i2);
    }

    private static double Holder(OptionType type, double x1, double x2, GeneralizedBlackScholesProcess p) =>
        AnalyticHolderExtensibleOptionEngine.Calculate(
            new HolderExtensibleOption(type, 1.0, Second, x2, new PlainVanillaPayoff(type, x1), new EuropeanExercise(First)),
            Context,
            p).NPV!.Value;
}
