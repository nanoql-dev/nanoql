// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic European engine's rho with a payoff discount curve of its own. QuantLib's rho moves the forward's rate
/// and the payoff discount's over one time, the discount curve's day counter from the risk-free curve's reference date
/// (<c>analyticeuropeanengine.cpp:81-86</c>), exact only where the two curves share their times. Against the value's
/// central difference with both curves' zero rates moved, it is 3.5% off at a week and 0.19% at a year with the
/// risk-free curve settling T+2 and the discount curve dated today, 1.6% and 0.19% the other way round, and 1.4% and
/// 1.1% with the discount curve on Act/360. nanoql moves each rate over its own curve's time: the identity −(T_r / T_q)
/// ρ_q − T_d V over QuantLib's own dividend rho and value, each time its own curve's, which meets the central
/// difference to 2e-9; no QuantLib function computes it. Where the two curves share their date and day counter, the rho
/// is QuantLib's.
/// </summary>
public class EuropeanRhoDiscountCurveTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData("risk-free curve T+2", 7, -0.4311651538154356)] // QuantLib -0.41605600291402167
    [InlineData("risk-free curve T+2", 365, -47.87695244655541)] // QuantLib -47.78564932320997
    [InlineData("discount curve T+2", 7, -0.9442967303543609)] // QuantLib -0.9591185016518639
    [InlineData("discount curve T+2", 365, -48.16116482849874)] // QuantLib -48.252267967801004
    [InlineData("discount curve on Act/360", 7, -0.9590480335607499)] // QuantLib -0.9720030542273284
    [InlineData("discount curve on Act/360", 365, -48.319679173757905)] // QuantLib -48.873837927661064
    public void EachRateMovesOverItsOwnCurvesTime(string market, int days, double rho)
    {
        OneAssetOptionResults results = Price(market, days);
        Assert.Equal(0.0, (results.Rho!.Value / rho) - 1.0, 1e-12);
    }

    [Theory]
    [InlineData(7, -0.9586981582810751, -34.46759631325759)]
    [InlineData(365, -48.23112092399896, -3.2547303389208873)]
    public void CurvesSharingTheirTimesAreQuantLibs(int days, double rho, double thetaOnAct360)
    {
        // the risk-free curve 5% and the discount curve 4% both dated today on Act/365: QuantLib's rho; on Act/360 the
        // discount curve's date is the risk-free curve's, so the theta is QuantLib's too
        Assert.Equal(rho, Price("shared", days).Rho!.Value, 1e-15 * Math.Abs(rho));
        Assert.Equal(thetaOnAct360, Price("discount curve on Act/360", days).Theta!.Value, 1e-15 * Math.Abs(thetaOnAct360));
    }

    // The risk-free curve 5% and the discount curve 4%; q 2% and volatility 25% dated today
    private static OneAssetOptionResults Price(string market, int days)
    {
        (YieldTermStructure riskFree, YieldTermStructure discount) = market switch
        {
            "risk-free curve T+2" => (Settling(0.05), new FlatForward(Today, 0.04, A365)),
            "discount curve T+2" => (new FlatForward(Today, 0.05, A365), Settling(0.04)),
            "discount curve on Act/360" => (new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.04, new Actual360())),
            _ => ((YieldTermStructure)new FlatForward(Today, 0.05, A365), (YieldTermStructure)new FlatForward(Today, 0.04, A365)),
        };
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, null, 0.25, A365));
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + days));
        return AnalyticEuropeanEngine.Calculate(put, Context, process, discount);
    }

    private static YieldTermStructure Settling(double rate) => FlatForward.Create(2, new TARGET(), rate, A365, Context);
}
