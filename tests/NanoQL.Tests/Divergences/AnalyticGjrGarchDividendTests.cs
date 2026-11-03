// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic GJR-GARCH engine with a dividend yield. QuantLib's engine prices on the rate r − q and discounts the
/// call's payoff at r − q too (<c>analyticgjrgarchengine.cpp</c>), so its price at (r, q) is its price at (r − q, 0): a
/// 180-day call at the money on the gjrgarchmodel test's model, r 5% and q 3%, is 3.49762, where the forward's measure
/// gives D_q times that, 3.44608. nanoql's call is D_q times QuantLib's engine at (r − q, 0), and its put follows by
/// parity, C − P = D_q·S − D_r·K. QuantLib's check: its engine at q = 0, where nanoql's is QuantLib's bit for bit (the
/// engine's vectors), and parity.
/// </summary>
public class AnalyticGjrGarchDividendTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date Maturity = Today + 180;

    [Fact]
    public void ThePriceIsTheDividendDiscountTimesTheZeroYieldPrice()
    {
        YieldTermStructure dividends = new FlatForward(Today, 0.03, A365);
        GJRGARCHModel withYield = Model(new FlatForward(Today, 0.05, A365), dividends);
        GJRGARCHModel without = Model(new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.0, A365));
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 50.0), new EuropeanExercise(Maturity));

        double expected = dividends.Discount(Maturity) * AnalyticGJRGARCHEngine.Calculate(call, Context, without).NPV!.Value;
        double calculated = AnalyticGJRGARCHEngine.Calculate(call, Context, withYield).NPV!.Value; // QuantLib: 3.497622167322453
        Assert.True(Math.Abs(calculated - expected) <= 1e-13 * expected, $"expected {expected}, calculated {calculated}");
    }

    [Fact]
    public void PutAndCallKeepParity()
    {
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365), dividends = new FlatForward(Today, 0.03, A365);
        GJRGARCHModel model = Model(riskFree, dividends);
        foreach (double strike in new[] { 40.0, 50.0, 60.0 })
        {
            double c = AnalyticGJRGARCHEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Maturity)), Context, model).NPV!.Value;
            double p = AnalyticGJRGARCHEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strike), new EuropeanExercise(Maturity)), Context, model).NPV!.Value;
            double parity = (dividends.Discount(Maturity) * 50.0) - (riskFree.Discount(Maturity) * strike);
            Assert.True(Math.Abs(c - p - parity) <= 1e-12 * 50.0, $"strike {strike}: {c - p} for {parity}");
        }
    }

    // the gjrgarchmodel test's set at lambda 0.1 from its stationary variance
    private static GJRGARCHModel Model(YieldTermStructure riskFree, YieldTermStructure dividends) =>
        new(new GJRGARCHProcess(riskFree, dividends, 50.0, 0.00017778348033623922, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0));
}
