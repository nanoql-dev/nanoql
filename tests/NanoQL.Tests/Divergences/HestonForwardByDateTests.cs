// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston helper's and <see cref="AnalyticHestonEngineOptimalAlpha"/>'s forward on a dividend curve that measures
/// time differently. QuantLib's helper reads both curves at the risk-free curve's time to the exercise date for its
/// Black price and its option's type (<c>hestonmodelhelper.cpp:68-69</c>, <c>:89-91</c>), where its engine reads them
/// by date (<c>analytichestonengine.cpp:648-653</c>): on a dividend curve on Act/360 beside a risk-free curve on
/// Act/365, the helper's one-year at-the-money market value is a put at the forward 101.0050, 7.2184670, while
/// QuantLib's analytic European engine prices that put at 7.2362869 on the forward 100.9629, so a calibration fits its
/// model's prices to the wrong market values. QuantLib's <c>OptimalAlpha</c> takes a time and reads both curves at it
/// (<c>:219-223</c>). nanoql's helper reads both curves by the exercise date, its market value the analytic engine's
/// price, and <see cref="AnalyticHestonEngineOptimalAlpha"/> gains a date form that reads the forward by date, as the
/// engine does; on curves that share the risk-free curve's day counter and reference date both are QuantLib's.
/// </summary>
public class HestonForwardByDateTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();

    [Fact]
    public void TheHelpersMarketValueReadsTheDividendCurveByDate()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        YieldTermStructure riskFree = new FlatForward(Today, 0.04, A365);
        YieldTermStructure dividends = new FlatForward(Today, 0.03, A360);

        var helper = new HestonModelHelper(
            new Period(1, TimeUnit.Years), new NullCalendar(), 100.0, 100.0, 0.2, riskFree, dividends, context);

        // QuantLib's analytic European engine on the same curves, the volatility on the risk-free curve's day counter:
        // the put 7.236286879658548; QuantLib's helper reads 7.218467016189528
        Assert.Equal(OptionType.Put, Assert.IsType<PlainVanillaPayoff>(helper.Option.Payoff).OptionType);
        Assert.Equal(7.236286879658548, helper.MarketValue, 1e-12);
        var process = new BlackScholesMertonProcess(100.0, dividends, riskFree, new BlackConstantVol(Today, null, 0.2, A365));
        Assert.Equal(
            AnalyticEuropeanEngine.Calculate(helper.Option, context, process).NPV!.Value, helper.MarketValue, 1e-12);
    }

    [Fact]
    public void OptimalAlphasDateFormReadsTheForwardByDate()
    {
        YieldTermStructure riskFree = new FlatForward(Today, 0.01, A365);
        YieldTermStructure dividends = new FlatForward(Today, 0.03, A360);
        Date maturity = Today + new Period(2, TimeUnit.Years);
        double t = A365.YearFraction(Today, maturity);

        var engine = new AnalyticHestonEngine(new HestonModel(new HestonProcess(riskFree, dividends, 75, 0.1, 1.2, 0.2, 0.2, -0.8)));

        // the dividend curve restated on the risk-free curve's day counter, with its discount at the maturity date
        double restatedRate = Math.Log(1.0 / dividends.Discount(maturity)) / t;
        var restated = new AnalyticHestonEngine(new HestonModel(
            new HestonProcess(riskFree, new FlatForward(Today, restatedRate, A365), 75, 0.1, 1.2, 0.2, 0.2, -0.8)));

        double byDate = new AnalyticHestonEngineOptimalAlpha(maturity, engine).AlphaGreaterZero(100).Alpha;
        Assert.Equal(new AnalyticHestonEngineOptimalAlpha(t, restated).AlphaGreaterZero(100).Alpha, byDate, 1e-8);

        // QuantLib's time form reads the Act/360 curve at the Act/365 time, another forward and another minimum
        double atTime = new AnalyticHestonEngineOptimalAlpha(t, engine).AlphaGreaterZero(100).Alpha;
        Assert.True(Math.Abs(byDate - atTime) > 1e-6, $"{byDate} against {atTime}");
    }
}
