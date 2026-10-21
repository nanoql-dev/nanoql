// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic engines' theta on structures dated differently. QuantLib's Black theta divides the payoff discount's,
/// the forward's and the variance's terms by one time, the volatility's in the European engine
/// (<c>analyticeuropeanengine.cpp:92-98</c>) and the risk-free curve's in the dividend engine
/// (<c>analyticdividendeuropeanengine.cpp:108-111</c>), exact only where the structures share it. On a risk-free curve
/// settling T+2 beside a dividend curve and a volatility dated today, the two engines read different thetas for the
/// same option: the dividend engine's −83.74 at a week where the decay is −34.44, and 0 on the risk-free curve's own
/// date, <c>BlackCalculator::theta</c>'s at a zero time. nanoql's engines divide each term by its own structure's time,
/// on the engine's day counter, none for a structure whose time is 0. That is QuantLib's <c>blackScholesTheta</c>
/// (<c>greeks.cpp:25-35</c>, the binomial engine's theta) at each structure's own rate on QuantLib's analytic value,
/// delta and gamma, to the 1e-11 its rates read over 1e-4 years carry, and the identity
/// −(r ρ / T_r + q ρ_q / T_q + σ ν / (2 T_v)) over QuantLib's own rho, dividend rho and vega to 1e-14. With no dividend
/// the two engines now read the same theta. Where the structures share their times, it is QuantLib's.
/// </summary>
public class AnalyticThetaMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData(7, -34.443097454110315)] // QuantLib: European -35.88990257770231, dividend -83.74310601463873
    [InlineData(91, -8.286344869147175)] // -8.39552972045846 and -8.781531086916322
    [InlineData(365, -3.145168863823064)] // -3.1713938525529173 and -3.206533950642146
    public void EachTermRunsOverItsOwnStructuresTime(int days, double theta)
    {
        // blackScholesTheta at r 5%, q 2% and volatility 25% on QuantLib's analytic value, delta and gamma
        BlackScholesMertonProcess process = Process(Settling(0.05));
        AssertTheta(theta, AnalyticEuropeanEngine.Calculate(Put(days), Context, process));
        AssertTheta(theta, AnalyticDividendEuropeanEngine.Calculate(Put(days), Context, process, ImmutableArray<Dividend>.Empty));
    }

    [Fact]
    public void OnTheRiskFreeCurvesDateItsTermIsNone()
    {
        // the put expiring on the risk-free curve's date: QuantLib's European engine, whose risk-free term is already
        // 0, and blackScholesTheta at r 0 to 1.2e-13; QuantLib's dividend engine reads 0
        BlackScholesMertonProcess process = Process(Settling(0.05));
        AssertTheta(-48.62118968053028, AnalyticEuropeanEngine.Calculate(Put(4), Context, process));
        AssertTheta(-48.62118968053028, AnalyticDividendEuropeanEngine.Calculate(Put(4), Context, process, ImmutableArray<Dividend>.Empty));
    }

    [Theory]
    [InlineData(true, 7, -34.444619334100864)] // QuantLib -35.85151753412396
    [InlineData(true, 365, -3.258352212845348)] // QuantLib -3.2802611006045455
    [InlineData(false, 7, -34.4838274877524)] // QuantLib -34.514741467869236
    [InlineData(false, 365, -3.26258528474645)] // QuantLib -3.266229410298668
    public void APayoffDiscountCurveRunsOverItsOwnTime(bool riskFreeSettles, int days, double theta)
    {
        // the risk-free curve 5% and the payoff's discount curve 4%, one settling T+2 and the other dated today:
        // blackScholesTheta at the discount curve's rate r_d and the dividend rate q + r_d - r_r, which keeps the
        // forward's drift, on QuantLib's analytic value, delta and gamma
        YieldTermStructure riskFree = riskFreeSettles ? Settling(0.05) : new FlatForward(Today, 0.05, A365);
        YieldTermStructure discount = riskFreeSettles ? new FlatForward(Today, 0.04, A365) : Settling(0.04);
        AssertTheta(theta, AnalyticEuropeanEngine.Calculate(Put(days), Context, Process(riskFree), discount));
    }

    [Theory]
    [InlineData(7, -34.408040577201824, -33.93669755559632)] // QuantLib -35.87474142220209 and -82.5610487524651
    [InlineData(365, -3.113614295585924, -3.070962044961459)] // -3.1401273524085918 and -3.1314289386900085
    public void EachEnginesThetaIsPerItsOwnDayCountersYear(int days, double european, double dividend)
    {
        // the risk-free curve 5% on Act/360 settling T+2 beside q and the volatility on Act/365 dated today:
        // blackScholesTheta on Act/365 at the risk-free curve's rate per Act/365 year, 5% x 365/360, on QuantLib's
        // analytic value, delta and gamma, per the volatility's Act/365 year in the European engine and per the
        // risk-free curve's Act/360 year, 360/365 of it, in the dividend engine, QuantLib's units
        BlackScholesMertonProcess process = Process(FlatForward.Create(2, new TARGET(), 0.05, new Actual360(), Context));
        AssertTheta(european, AnalyticEuropeanEngine.Calculate(Put(days), Context, process));
        AssertTheta(dividend, AnalyticDividendEuropeanEngine.Calculate(Put(days), Context, process, ImmutableArray<Dividend>.Empty));
    }

    private static YieldTermStructure Settling(double rate) => FlatForward.Create(2, new TARGET(), rate, A365, Context);

    // q 2% and volatility 25% dated today
    private static BlackScholesMertonProcess Process(YieldTermStructure riskFree) =>
        new(100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, null, 0.25, A365));

    private static VanillaOption Put(int days) =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + days));

    private static void AssertTheta(double theta, OneAssetOptionResults results) =>
        Assert.Equal(0.0, (results.Theta!.Value / theta) - 1.0, 1e-10);
}
