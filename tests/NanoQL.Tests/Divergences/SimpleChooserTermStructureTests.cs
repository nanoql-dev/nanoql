// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The simple chooser engine on term structures. QuantLib evaluates Rubinstein's formula on the zero rates and the
/// volatility at maturity, its times from the evaluation date (<c>analyticsimplechooserengine.cpp:49-60</c>), so on a
/// rising market it is +22% off; nanoql prices Rubinstein's decomposition at the choosing date,
/// C(K, T) + D_q(t, T)·P(K·D_r(t, T)/D_q(t, T), t), each by the analytic European engine (the S-exo plan's ER4). The
/// checks: the option's exact value at the choosing date, the larger of the call and the put there over the lognormal
/// spot, within 1e-9; the European engine's two options, bit for bit; and QuantLib's value on a flat market.
/// </summary>
public class SimpleChooserTermStructureTests
{
    private static readonly Date Choosing = Today + 90, Maturity = Today + 365;

    [Fact]
    public void ARisingMarketMeetsTheExactValueAndTheEuropeanEngine()
    {
        Check(Rising(100.0));
    }

    [Fact]
    public void AFallingMarketMeetsTheExactValueAndTheEuropeanEngine()
    {
        Check(Falling(100.0));
    }

    [Fact]
    public void AFlatMarketIsQuantLibs()
    {
        // QuantLib's engine on the flat market (r 3%, q 0, σ 25%)
        Assert.Equal(14.888443248012294, Chooser(Flat(100.0)), 1e-13 * 14.888443248012294);
    }

    private static void Check(GeneralizedBlackScholesProcess p)
    {
        double Payoff(double s) =>
            Math.Max(
                ForwardBlack(p, OptionType.Call, 100.0, Choosing, Maturity, s),
                ForwardBlack(p, OptionType.Put, 100.0, Choosing, Maturity, s));
        double kink = Kink(
            s => ForwardBlack(p, OptionType.Call, 100.0, Choosing, Maturity, s)
                - ForwardBlack(p, OptionType.Put, 100.0, Choosing, Maturity, s),
            1.0,
            1000.0);
        double exact = Exact(p, Choosing, Payoff, kink);
        double calculated = Chooser(p);
        Assert.Equal(exact, calculated, 1e-9 * exact);

        // the decomposition, each option by the European engine
        double dq = p.DividendYield.Discount(Maturity) / p.DividendYield.Discount(Choosing);
        double dr = p.RiskFreeRate.Discount(Maturity) / p.RiskFreeRate.Discount(Choosing);
        double call = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Maturity)), Context, p).NPV!.Value;
        double put = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0 * dr / dq), new EuropeanExercise(Choosing)), Context, p).NPV!.Value;
        Assert.Equal(call + (dq * put), calculated);
    }

    private static double Chooser(GeneralizedBlackScholesProcess p) =>
        AnalyticSimpleChooserEngine.Calculate(
            new SimpleChooserOption(Choosing, 100.0, new EuropeanExercise(Maturity)), Context, p).NPV!.Value;
}
