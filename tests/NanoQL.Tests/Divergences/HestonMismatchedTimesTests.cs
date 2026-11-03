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
/// The Heston model on curves dated differently. QuantLib's analytic Heston engine runs the model's time from the
/// risk-free curve's reference date (<c>analytichestonengine.cpp:644-653</c>), so beside a risk-free curve settling T+2
/// and a dividend curve dated today the variance accrues only from T+2, where the dividend accrues from today: a
/// one-year put reads 7.816084368797552 against 7.863347278811837 on the market restated from today, the risk-free
/// curve accruing nothing before T+2, and a put expiring before or on the curve's date throws "negative time" or reads
/// 0/0. Its calibration helper and its optimal alpha's date form read the same time. nanoql's engine, helper and alpha
/// run the model's time on the risk-free curve's day counter from the earliest curve date, each curve accruing nothing
/// before its own, and read the risk-free discount by date: QuantLib's engine, helper and alpha on the restated market.
/// Where the dividend curve isn't dated before the risk-free curve, the time is QuantLib's.
/// </summary>
public class HestonMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData(1, 0.524557258064923)] // QuantLib as given throws "negative time"
    [InlineData(4, 1.0532696351456703)] // reads 0/0
    [InlineData(365, 7.863347278811837)] // 7.816084368797552
    public void TheModelsTimeRunsFromTheEarliestCurveDate(int days, double npv)
    {
        // QuantLib's engine (Gauss-Laguerre, 192) on the market restated from today
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + days));
        Assert.Equal(0.0, (AnalyticHestonEngine.Calculate(put, Context, Model(), 192).NPV!.Value / npv) - 1.0, 1e-12);
    }

    [Fact]
    public void TheHelpersMaturityRunsFromTheEarlierCurveDate()
    {
        // a put at 100, quoted at 25%, 365 days from the risk-free curve's date on NullCalendar: QuantLib's helper on
        // the restated market, 369 days from today, the same exercise date, its market value and its model value on the
        // restated model; QuantLib as given 8.22683704745399 and 7.847950201748561
        HestonModel model = Model();
        var helper = new HestonModelHelper(
            new Period(365, TimeUnit.Days), new NullCalendar(), 100.0, 100.0, 0.25, model.Process.RiskFreeRate, model.Process.DividendYield, Context)
            .SetPricingEngine<HestonModel>((o, c, m) => AnalyticHestonEngine.Calculate(o, c, m, 192));
        Assert.Equal(Today + 369, helper.ExerciseDate);
        Assert.Equal(369.0 / 365.0, helper.Maturity);
        Assert.Equal(0.0, (helper.MarketValue / 8.287344193413269) - 1.0, 1e-12);
        Assert.Equal(0.0, (helper.ModelValue(model, Context) / 7.894938166584955) - 1.0, 1e-12);
    }

    [Theory]
    [InlineData(80.0, -4.9840719295465385)] // QuantLib's time form at a year on the market as given -4.985062690679628
    [InlineData(100.0, 6.2029479490250194)] // 6.196096399249798
    [InlineData(120.0, 8.812985674407296)] // 8.804555938714682
    public void TheOptimalAlphasDateFormReadsTheEnginesTime(double strike, double alpha)
    {
        // QuantLib's time form at a year from today on the restated market, its curves read by date there
        var search = new AnalyticHestonEngineOptimalAlpha(Today + 365, new AnalyticHestonEngine(Model(), 192));
        Assert.Equal(alpha, search.Evaluate(strike), 1e-10);
    }

    // The risk-free curve 5% settling T+2 (19 October), q 2% today; v0 0.0625, kappa 1.5, theta 0.0625, sigma 0.4,
    // rho -0.6, spot 100
    private static HestonModel Model()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);
        return new HestonModel(new HestonProcess(settling, new FlatForward(Today, 0.02, A365), 100.0, 0.0625, 1.5, 0.0625, 0.4, -0.6));
    }
}
