// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A year-on-year cap's volatility quote is its value: QuantLib's <c>testVolatilityQuoteObservability</c> prices a cap
/// on a constant volatility built on a quote of 1%, moves the quote to 2% and checks that the price moves to that of a
/// cap priced on a volatility of 2%. Here the moved quote is a volatility built from the new value, which answers the
/// moved price; the cap and the first volatility are unchanged.
/// </summary>
public class YoYInflationCapFloorQuoteTests
{
    [Fact]
    public void ACapPricedOnTheNewValueAnswersIt()
    {
        var vars = new InflationCapFloorTests.CommonVars();
        ImmutableArray<CashFlow> leg = vars.MakeYoYLeg(vars.EvaluationDate, 2);
        var cap = new YoYInflationCap(leg, [0.0295]);

        double quoted = Npv(cap, Surface(vars, 0.01), vars);
        double moved = Npv(cap, Surface(vars, 0.02), vars);
        Assert.NotEqual(quoted, moved);

        var other = new YoYInflationCap(vars.MakeYoYLeg(vars.EvaluationDate, 2), [0.0295]);
        double expected = vars.MakeEngine(0.02, 0)(other, vars.Context).NPV!.Value;
        Assert.True(Math.Abs(moved - expected) / expected < 1.0e-10, $"{moved} against {expected}");
        Assert.Equal(quoted, Npv(cap, Surface(vars, 0.01), vars));
    }

    private static YoYOptionletVolatilitySurface Surface(InflationCapFloorTests.CommonVars vars, double volatility) =>
        ConstantYoYOptionletVolatility.Create(
            volatility, vars.SettlementDays, vars.Calendar, vars.Convention, vars.Dc, vars.ObservationLag, vars.Frequency, false, vars.Context);

    private static double Npv(YoYInflationCap cap, YoYOptionletVolatilitySurface volatility, InflationCapFloorTests.CommonVars vars) =>
        YoYInflationBlackCapFloorEngine.Calculate(cap, vars.Context, vars.Iir, volatility, vars.NominalTS).NPV!.Value;
}
