// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Black-Scholes and Heston processes on structures that measure time differently. A process's times are the
/// risk-free curve's (<c>blackscholesprocess.cpp:149-152</c>), and QuantLib reads the dividend curve, the volatility
/// and the local volatility at those times (<c>blackscholesprocess.cpp:65-130</c>, <c>hestonprocess.cpp:474-475</c>),
/// each of which measures time from its own reference date in its own day counter: on a dividend curve and a volatility
/// on Act/360 beside a risk-free curve on Act/365, the Black-Scholes process evolves a year 2.8e-4 away from the same
/// market restated on one day counter. nanoql reads each structure at the instants the times denote, on its own clock,
/// as the finite-difference engine does (<c>FdmMismatchedTimesTests</c>): QuantLib's process on the market restated on
/// the risk-free curve's day counter is the check. A structure accrues nothing before its own reference date, as
/// QuantLib's analytic engines count it; the days before the process's time 0, the risk-free curve's reference date,
/// aren't the process's, but an engine's that runs from the earliest date, as the finite-difference engine does. Every
/// read is at the instants, so the exact evolve and the discretization's agree. A structure on the risk-free curve's
/// day counter and reference date is read as QuantLib reads it.
/// </summary>
public class ProcessMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends360 = new FlatForward(Today, 0.03, A360);

    [Fact]
    public void TheBlackScholesProcessReadsEachStructureAtItsInstants()
    {
        // QuantLib's process on the dividend curve and the volatility restated on Act/365, the same discounts and
        // variances at every date (the wheel): drift(0.5, 100), diffusion(0.5, 100), expectation(0, 100, 1),
        // variance(0, 100, 1) and evolve(0, 100, 1, 0.3); on the mixed market its evolve reads 106.18365465453596 with
        // the constant volatility
        var constant = new BlackScholesMertonProcess(100.0, Dividends360, RiskFree, new BlackConstantVol(Today, new NullCalendar(), 0.2, A360));
        Read(constant, -0.0006944444450587145, 0.20138409955990955, 101.97763446819235, 0.04055555555555556, 106.15401076906845);

        // a variance curve on Act/360, whose local volatility is its variance's slope
        var curve = new BlackScholesMertonProcess(
            100.0, Dividends360, RiskFree, new BlackVarianceCurve(Today, [Today + 91, Today + 365, Today + 730], [0.22, 0.24, 0.25], A360, true));
        Read(curve, -0.011165622466145712, 0.24798772469162572, 101.97763446819235, 0.05839999999999999, 106.4900757288676);
    }

    [Fact]
    public void TheHestonProcessReadsTheDividendCurveAtItsInstants()
    {
        // QuantLib's process on the dividend curve restated on Act/365: a year's evolve with zero draws, and the drift
        var process = new HestonProcess(RiskFree, Dividends360, 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
        Vector x = process.Evolve(0.0, new Vector([100.0, 0.04]), 1.0, new Vector([0.0, 0.0]));
        Assert.Equal(103.47653954139122, x[0], 1e-12);
        Assert.Equal(0.03209162259875181, x[1], 1e-12);
        Assert.Equal(-0.0004166666672809384, process.Drift(0.5, new Vector([100.0, 0.04]))[0], 1e-12);
    }

    [Fact]
    public void EachStructureAccruesNothingBeforeItsOwnDate()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        double[] discounts = [1.0, 0.9975099546340253, 0.9900769587737215, 0.9607367945979871, 0.9230657660548007];
        var vol = new BlackConstantVol(Today, new NullCalendar(), 0.25, A365);
        Date maturity = Today + 365;

        // a dividend curve dated today beside a risk-free curve settling T+2: the expectation from time 0, 19 October,
        // is the forward between the instants, the dividend curve's days before then not the process's
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, context);
        var today = new DiscountCurve([Today, Today + 91, Today + 182, Today + 365, Today + 730], discounts, A365);
        var early = new BlackScholesMertonProcess(100.0, today, settling, vol);
        double forward = 100.0 * today.Discount(maturity) / today.Discount(settling.ReferenceDate) / settling.Discount(maturity);
        Assert.Equal(forward, early.Expectation(0.0, 100.0, early.Time(maturity)), 1e-12);

        // the exact evolve, on a constant volatility, and the Euler discretization's, on a flat variance surface, agree
        var surface = new BlackVarianceSurface(
            Today, new NullCalendar(), [Today + 91, Today + 365], [80.0, 120.0], new Matrix(2, 2, 0.25), A365);
        var exact = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), settling, vol);
        var euler = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), settling, surface);
        Assert.Equal(exact.Evolve(0.0, 100.0, 0.01, 0.0), euler.Evolve(0.0, 100.0, 0.01, 0.0), 1e-12);

        // a dividend curve dated four days later accrues nothing before its date: the analytic engine's forward, and
        // no dividend in the drift before then
        Date later = Today + 4;
        var late = new DiscountCurve([later, later + 91, later + 182, later + 365, later + 730], discounts, A365);
        var process = new BlackScholesMertonProcess(100.0, late, RiskFree, vol);
        Assert.Equal(100.0 * late.Discount(maturity) / RiskFree.Discount(maturity), process.Expectation(0.0, 100.0, 1.0), 1e-12);
        Assert.Equal(0.05 - (0.5 * 0.25 * 0.25), process.Drift(0.0, 100.0), 1e-12);
    }

    private static void Read(GeneralizedBlackScholesProcess p, double drift, double diffusion, double expectation, double variance, double evolve)
    {
        Assert.Equal(drift, p.Drift(0.5, 100.0), 1e-12);
        Assert.Equal(diffusion, p.Diffusion(0.5, 100.0), 1e-12);
        Assert.Equal(expectation, p.Expectation(0.0, 100.0, 1.0), 1e-12);
        Assert.Equal(variance, p.Variance(0.0, 100.0, 1.0), 1e-12);
        Assert.Equal(evolve, p.Evolve(0.0, 100.0, 1.0, 0.3), 1e-12);
    }
}
