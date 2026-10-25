// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference Heston and Bates engines on structures dated or counted differently (the S-hes plan's
/// HR7 (ii), the mismatched-dates class of master Revision 58). QuantLib's engines run from the risk-free curve's
/// reference date (<c>process-&gt;time</c>) and read the dividend curve, the leverage function and the Bates spread
/// curve at the risk-free curve's times (<c>fdmhestonop.cpp:55-89</c>, <c>fdmbatesop.cpp:45-58</c>). nanoql's run on
/// <see cref="FdmHestonOp.Clock"/>, from the earliest reference date of the structures read, on the risk-free curve's day
/// counter, each structure read at the instants its times denote, nothing accruing before its own date, the leverage at
/// its instant. QuantLib's check: its engines on the market restated on one clock, which nanoql's engines on the
/// original market reproduce to rounding [executed on the wheel: <c>scratchpad\c1\oracle_c1.py</c>].
/// </summary>
public class FdHestonMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();

    private static readonly VanillaOption Put =
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void ADividendCurveOnAnotherDayCounterIsReadAtItsOwnTimes()
    {
        // q 2% on Act/365 beside r 5% on Act/360; QuantLib's restated market: q 2%·360/365 on Act/360
        HestonModel model = Model(new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.02, A365));
        AssertClose(5.96412115284696, FdHestonVanillaEngine.Calculate(Put, Context, model).NPV!.Value); // as given 5.972994574176386
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        // r 5% settling T+2, q 2% today; QuantLib's restated market: every curve today, the risk-free discount 1 to T+2
        // and 5% after (a log-linear discount curve)
        HestonModel model = Model(new FlatForward(Today + 2, 0.05, A365), new FlatForward(Today, 0.02, A365));
        AssertClose(5.954563817139318, FdHestonVanillaEngine.Calculate(Put, Context, model).NPV!.Value); // as given 5.932678548597246
    }

    [Fact]
    public void ALeverageFunctionOnItsOwnClockIsReadAtItsInstant()
    {
        // a leverage surface on Act/360 beside curves on Act/365; QuantLib's restated market: the surface on Act/365,
        // its values at the same dates, so the same at each instant
        HestonModel model = Model(new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365));
        double npv = FdHestonVanillaEngine.Calculate(Put, Context, model, 100, 100, 50, 0, null, Leverage(A360)).NPV!.Value;
        AssertClose(5.189448892356328, npv); // as given 5.178943154749274
    }

    [Fact]
    public void TheBatesSpreadedDividendCurveIsReadAtItsOwnTimes()
    {
        // FD Bates, q 2% on Act/365 beside r 5% on Act/360; QuantLib's restated market: q on Act/360
        var process = new BatesProcess(
            new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6, 1.0, -0.1, 0.1);
        double npv = FdBatesVanillaEngine.Calculate(Put, Context, new BatesModel(process), 50, 100, 30).NPV!.Value;
        AssertClose(7.704985734203767, npv); // as given 7.714352293491298
    }

    // v0 = theta 0.04, kappa 1.5, sigma 0.4, rho -0.6, spot 100
    private static HestonModel Model(YieldTermStructure riskFree, YieldTermStructure dividends) =>
        new(new HestonProcess(riskFree, dividends, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6));

    // oracle_c1.py's leverage surface: 0.8 + 0.1·j + 0.002·√|K − 100| at the j-th date
    private static FixedLocalVolSurface Leverage(DayCounter dc)
    {
        Date[] dates = [Today + 90, Today + 180, Today + 365, Today + 730];
        double[] strikes = [10.0, 50.0, 80.0, 100.0, 120.0, 150.0, 300.0, 1000.0];
        var values = new NanoQL.Numerics.Matrix(strikes.Length, dates.Length);
        for (int i = 0; i < strikes.Length; ++i)
        {
            for (int j = 0; j < dates.Length; ++j)
            {
                values[i, j] = 0.8 + (0.1 * j) + (0.002 * Math.Pow(Math.Abs(strikes[i] - 100.0), 0.5));
            }
        }

        return new FixedLocalVolSurface(Today, dates, strikes, values, dc);
    }

    private static void AssertClose(double expected, double calculated) =>
        Assert.True(
            Math.Abs(calculated - expected) <= 1e-11 * Math.Abs(expected),
            $"expected {expected}, calculated {calculated}");
}
