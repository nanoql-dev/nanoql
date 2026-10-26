// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.ExoticTermStructureMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The complex chooser engine on term structures. QuantLib evaluates Rubinstein's formula on one zero rate, dividend
/// yield and volatility per maturity and solves its critical value on the times between the dates
/// (<c>analyticcomplexchooserengine.cpp:161-179</c>), so on a rising market it is −19.5% off with #2891's defect; nanoql
/// reads the variances to each date, the correlations from their ratios and the discounts by date, its critical spot
/// solving the forward Black values from the choosing date, on West's bivariate normal (the S-exo plan's ER4, ER7). The
/// check is the option's exact value at the choosing date, the larger of the call and the put there over the lognormal
/// spot, within the critical value's accuracy, 1e-4 (QuantLib's Newton stops at 0.001 in the difference of the two);
/// on a flat market, the exact value likewise and Haug's, QuantLib's master case, within its 1e-4.
/// </summary>
public class ComplexChooserTermStructureTests
{
    private static readonly Date Choosing = Today + 90, CallMaturity = Today + 365, PutMaturity = Today + 300;

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
    public void AFlatMarketMeetsTheExactValueAndHaugs()
    {
        Check(Flat(100.0));

        // QuantLib's master case (#2891): Haug's 6.0508 on Act/360 curves from today
        var today = new Date(15, Month.October, 2026);
        DayCounter dc = new Actual360();
        var p = new BlackScholesMertonProcess(
            50.0, new TermStructures.FlatForward(today, 0.05, dc), new TermStructures.FlatForward(today, 0.10, dc),
            new TermStructures.BlackConstantVol(today, new NullCalendar(), 0.35, dc));
        var option = new ComplexChooserOption(
            today + 90, 55.0, 48.0, new EuropeanExercise(today + 180), new EuropeanExercise(today + 210));
        double haug = AnalyticComplexChooserEngine.Calculate(option, Context, p).NPV!.Value;
        Assert.Equal(6.0508, haug, 1e-4);
        double exact = ExactValue(p, today + 90, 55.0, 48.0, today + 180, today + 210);
        Assert.Equal(exact, haug, 1e-4 * exact);
    }

    private static void Check(GeneralizedBlackScholesProcess p)
    {
        double exact = ExactValue(p, Choosing, 105.0, 95.0, CallMaturity, PutMaturity);
        double calculated = AnalyticComplexChooserEngine.Calculate(
            new ComplexChooserOption(Choosing, 105.0, 95.0, new EuropeanExercise(CallMaturity), new EuropeanExercise(PutMaturity)),
            Context,
            p).NPV!.Value;
        Assert.Equal(exact, calculated, 1e-4 * exact);
    }

    private static double ExactValue(
        GeneralizedBlackScholesProcess p, Date choosing, double xc, double xp, Date callMaturity, Date putMaturity)
    {
        double Call(double s) => ForwardBlack(p, OptionType.Call, xc, choosing, callMaturity, s);
        double Put(double s) => ForwardBlack(p, OptionType.Put, xp, choosing, putMaturity, s);
        double kink = Kink(s => Call(s) - Put(s), 1e-3, 1e4);
        return Exact(p, choosing, s => Math.Max(Call(s), Put(s)), kink);
    }
}
