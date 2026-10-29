// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Vecer's Asian engine at its grid's upper end and on mixed clocks (the S-xeq plan's XER5 (xxi), (xxii)). QuantLib
/// interpolates between the nodes below and above the initial point, so where the point lies on the grid's upper end it
/// reads one node past the grid (<c>continuousarithmeticasianvecerengine.cpp:185-190</c>); nanoql takes the last node's
/// value there, the interpolation's own limit. QuantLib measures the time from the evaluation date on the risk-free day
/// counter beside each structure's own zero rate and volatility; nanoql reads each on the engine's clock, so the market
/// restated on one day counter prices the same. Checks: the price continuous as the point reaches the upper end; the
/// restated market's price, against QuantLib's reading of the unrestated rates and volatility.
/// </summary>
public class VecerEngineTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtTheGridsUpperEndThePriceIsTheLastNode()
    {
        // r = q = 0 and K = S put the point at 1 − K/S = 0, the upper end; 128 steps of 2⁻⁷ make its index 128 exactly,
        // where QuantLib reads u[129]
        double Price(double strike) => ContinuousArithmeticAsianVecerEngine.Calculate(
            Option(strike, Today + 360), Context, Process(0.0, A360, 0.0, A360, 0.25, A360), 0.0, Today, 100, 128, -1.0, 0.0).NPV!.Value;
        double atEnd = Price(100.0), below = Price(100.0 + 1e-7);
        Assert.True(double.IsFinite(atEnd));
        Assert.True(Math.Abs(atEnd - below) <= 1e-6, $"{atEnd} against {below}");
    }

    [Fact]
    public void AMarketOnMixedDayCountersIsTheMarketRestatedOnOne()
    {
        // the risk-free curve on Actual/360, the dividend curve and the volatility on Actual/365F, against the dividend
        // rate and the volatility restated on Actual/360 at the same accrual and variance to the maturity
        Date maturity = Today + 365;
        double ratio = A365.YearFraction(Today, maturity) / A360.YearFraction(Today, maturity);
        double Price(double q, DayCounter qDc, double sigma, DayCounter volDc) => ContinuousArithmeticAsianVecerEngine.Calculate(
            Option(100.0, maturity), Context, Process(0.05, A360, q, qDc, sigma, volDc), 0.0, Today, 200, 200, -1.0, 1.0).NPV!.Value;
        double mixed = Price(0.02, A365, 0.25, A365);
        double restated = Price(0.02 * ratio, A360, 0.25 * Math.Sqrt(ratio), A360);
        double quantLibs = Price(0.02, A360, 0.25, A360);
        Assert.Equal(restated, mixed, 1e-12);
        Assert.True(Math.Abs(quantLibs - mixed) > 1e-3, $"QuantLib's reading {quantLibs} against {mixed}");
    }

    // an arithmetic average-price call
    private static ContinuousAveragingAsianOption Option(double strike, Date maturity) =>
        new(AverageType.Arithmetic, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity));

    // spot 100
    private static BlackScholesMertonProcess Process(double r, DayCounter rDc, double q, DayCounter qDc, double sigma, DayCounter volDc) =>
        new(100.0, new FlatForward(Today, q, qDc), new FlatForward(Today, r, rDc), new BlackConstantVol(Today, new NullCalendar(), sigma, volDc));
}
