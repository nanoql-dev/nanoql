// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/doublebinaryoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testHaugValues' binomial checks, which wait for S-xeq's BinomialDoubleBarrierEngine
// Moved: testHaugValues line 171 (Divergences/DoubleBinaryKnockedInTests.cs); testHaugValues line 172 (Divergences/DoubleBinaryKnockedInTests.cs)

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's options hold their engines and its quotes notify them; here each pricing passes the engine's arguments,
// and a quote set anew is a process built at the new value.
[Trait("Oracle", "QuantLib")]
public class DoubleBinaryOptionTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestHaugValues()
    {
        // Testing cash-or-nothing double barrier options against Haug's values...

        DoubleBinaryOptionData[] values =
        [
            /* The data below are from
              "Option pricing formulas 2nd Ed.", E.G. Haug, McGraw-Hill 2007 pag. 181
              Note: book uses cost of carry b, instead of dividend rate q
            */
            //    barrierType, bar_lo, bar_hi, cash, spot, q, r, t, vol, value, tol, moved
            new(DoubleBarrierType.KnockOut, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 9.8716, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 8.9307, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 6.3272, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 1.9094, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 9.7961, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 7.2300, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 3.7100, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 0.4271, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 8.9054, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 3.6752, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 0.7960, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 0.0059, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 3.6323, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 0.0911, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 0.0002, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 0.0000, 1e-4, false),
            new(DoubleBarrierType.KIKO, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.0000, 1e-4, false),
            new(DoubleBarrierType.KIKO, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 0.2402, 1e-4, false),
            new(DoubleBarrierType.KIKO, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 1.4076, 1e-4, false),
            new(DoubleBarrierType.KIKO, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 3.8160, 1e-4, false),
            new(DoubleBarrierType.KIKO, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.0075, 1e-4, false),
            new(DoubleBarrierType.KIKO, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 0.9910, 1e-4, false),
            new(DoubleBarrierType.KIKO, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 2.8098, 1e-4, false),
            new(DoubleBarrierType.KIKO, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 4.6612, 1e-4, false),
            new(DoubleBarrierType.KIKO, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.2656, 1e-4, false),
            new(DoubleBarrierType.KIKO, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 2.7954, 1e-4, false),
            new(DoubleBarrierType.KIKO, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 4.4024, 1e-4, false),
            new(DoubleBarrierType.KIKO, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 4.9266, 1e-4, false),
            new(DoubleBarrierType.KIKO, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 2.6285, 1e-4, false),
            new(DoubleBarrierType.KIKO, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 4.7523, 1e-4, false),
            new(DoubleBarrierType.KIKO, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 4.9096, 1e-4, false),
            new(DoubleBarrierType.KIKO, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 4.9675, 1e-4, false),

            // following values calculated with haug's VBA code
            new(DoubleBarrierType.KnockIn, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.0042, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 0.9450, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 3.5486, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 7.9663, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.0797, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 2.6458, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 6.1658, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 9.4486, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.9704, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 6.2006, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 9.0798, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 9.8699, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 6.2434, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 9.7847, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 9.8756, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 9.8758, 1e-4, false),
            new(DoubleBarrierType.KOKI, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.0041, 1e-4, false),
            new(DoubleBarrierType.KOKI, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 0.7080, 1e-4, false),
            new(DoubleBarrierType.KOKI, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 2.1581, 1e-4, false),
            new(DoubleBarrierType.KOKI, 80.00, 120.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 4.2061, 1e-4, false),
            new(DoubleBarrierType.KOKI, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.0723, 1e-4, false),
            new(DoubleBarrierType.KOKI, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 1.6663, 1e-4, false),
            new(DoubleBarrierType.KOKI, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 3.3930, 1e-4, false),
            new(DoubleBarrierType.KOKI, 85.00, 115.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 4.8679, 1e-4, false),
            new(DoubleBarrierType.KOKI, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 0.7080, 1e-4, false),
            new(DoubleBarrierType.KOKI, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 3.4424, 1e-4, false),
            new(DoubleBarrierType.KOKI, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 4.7496, 1e-4, false),
            new(DoubleBarrierType.KOKI, 90.00, 110.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 5.0475, 1e-4, false),
            new(DoubleBarrierType.KOKI, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.10, 3.6524, 1e-4, false),
            new(DoubleBarrierType.KOKI, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.20, 5.1256, 1e-4, false),
            new(DoubleBarrierType.KOKI, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.30, 5.0763, 1e-4, false),
            new(DoubleBarrierType.KOKI, 95.00, 105.00, 10.00, 100.00, 0.02, 0.05, 0.25, 0.50, 5.0275, 1e-4, false),

            // degenerate cases
            new(DoubleBarrierType.KnockOut, 95.00, 105.00, 10.00, 80.00, 0.02, 0.05, 0.25, 0.10, 0.0000, 1e-4, false),
            new(DoubleBarrierType.KnockOut, 95.00, 105.00, 10.00, 110.00, 0.02, 0.05, 0.25, 0.10, 0.0000, 1e-4, false),
            new(DoubleBarrierType.KnockIn, 95.00, 105.00, 10.00, 80.00, 0.02, 0.05, 0.25, 0.10, 10.0000, 1e-4, true),
            new(DoubleBarrierType.KnockIn, 95.00, 105.00, 10.00, 110.00, 0.02, 0.05, 0.25, 0.10, 10.0000, 1e-4, true),
            new(DoubleBarrierType.KIKO, 95.00, 105.00, 10.00, 80.00, 0.02, 0.05, 0.25, 0.10, 10.0000, 1e-4, false),
            new(DoubleBarrierType.KIKO, 95.00, 105.00, 10.00, 110.00, 0.02, 0.05, 0.25, 0.10, 0.0000, 1e-4, false),
            new(DoubleBarrierType.KOKI, 95.00, 105.00, 10.00, 80.00, 0.02, 0.05, 0.25, 0.10, 0.0000, 1e-4, false),
            new(DoubleBarrierType.KOKI, 95.00, 105.00, 10.00, 110.00, 0.02, 0.05, 0.25, 0.10, 10.0000, 1e-4, false),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (DoubleBinaryOptionData value in values)
        {
            var payoff = new CashOrNothingPayoff(OptionType.Call, 0, value.Cash);

            Date exDate = today + TimeToDays(value.T);
            Exercise exercise = value.BarrierType is DoubleBarrierType.KIKO or DoubleBarrierType.KOKI
                ? new AmericanExercise(today, exDate)
                : new EuropeanExercise(exDate);

            YieldTermStructure rTS = FlatRate(today, value.R, dc);
            var stochProcess = new BlackScholesMertonProcess(
                value.S, FlatRate(today, value.Q, dc), rTS, FlatVol(today, value.V, dc));

            // checking with analytic engine
            var opt = new DoubleBarrierOption(value.BarrierType, value.BarrierLo, value.BarrierHi, 0, payoff, exercise);

            double calculated = AnalyticDoubleBarrierBinaryEngine.Calculate(opt, RunnerContext, stochProcess).NPV!.Value;

            // Moved: QuantLib's check at lines 171-172 expects the cash undiscounted from an option already knocked in,
            // which pays at expiry; the European cash-or-nothing it is pays cash x D(T) (the S-bar plan's BR5 (vi),
            // Divergences/DoubleBinaryKnockedInTests.cs)
            double expected = value.Moved ? value.Cash * rTS.Discount(exDate) : value.Result;
            double error = Math.Abs(calculated - expected);
            Assert.False(
                error > value.Tol,
                $"{DoubleBarrierTypeFunctions.ToString(value.BarrierType)} option: barrier low {value.BarrierLo}, "
                + $"barrier high {value.BarrierHi}, cash {value.Cash}, spot {value.S}, dividend yield {value.Q}, "
                + $"risk-free rate {value.R}, reference date {today}, maturity {exDate}, volatility {value.V}: expected "
                + $"value {expected}, calculated value {calculated}, error {error}, tolerance {value.Tol}");

            // the binomial engine's check waits for S-xeq's BinomialDoubleBarrierEngine
        }
    }

    [Fact]
    public void TestPdeDoubleBarrierWithAnalytical()
    {
        // Testing cash-or-nothing double barrier options against PDE Heston version...

        DayCounter dc = new Actual360();
        var todaysDate = new Date(30, January, 2023);
        Date maturityDate = todaysDate + new Period(1, TimeUnit.Years);
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        const double spot = 100;
        const double r = 0.075;
        const double q = 0.03;
        const double vol = 0.4;
        const double kappa = 1.0;
        const double theta = vol * vol;
        const double rho = 0.0;
        const double sigma = 1e-4;
        const double v0 = theta;

        YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), r, dc, context);
        YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), q, dc, context);

        var hestonModel = new HestonModel(new HestonProcess(rTS, qTS, spot, v0, kappa, theta, sigma, rho));

        var bsProcess = new BlackScholesMertonProcess(spot, qTS, rTS, FlatVol(todaysDate, vol, dc));

        var europeanExercise = new EuropeanExercise(maturityDate);

        const double tol = 5e-3;
        for (int i = 5; i < 18; i += 2)
        {
            double dist = 10.0 + (5.0 * i);

            double barrierLo = Math.Max(spot - dist, 1e-2);
            double barrierHi = spot + dist;
            var doubleBarrier = new DoubleBarrierOption(
                DoubleBarrierType.KnockOut,
                barrierLo,
                barrierHi,
                0.0,
                new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0),
                europeanExercise);

            double bsNPV = AnalyticDoubleBarrierBinaryEngine.Calculate(doubleBarrier, context, bsProcess).NPV!.Value;

            double slvNPV = FdHestonDoubleBarrierEngine.Calculate(
                doubleBarrier, context, hestonModel, 201, 101, 3, 0, FdmSchemeDesc.Hundsdorfer()).NPV!.Value;

            double diff = slvNPV - bsNPV;
            Assert.False(
                Math.Abs(diff) > tol,
                "Failed to reproduce price difference for a Double-No-Touch option between Black-Scholes and Heston "
                + $"Stochastic Local Volatility model: barrier low {barrierLo}, barrier high {barrierHi}, Black-Scholes "
                + $"price {bsNPV}, Heston SLV price {slvNPV}, diff {diff}, tolerance {tol}");
        }
    }

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private sealed record DoubleBinaryOptionData(
        DoubleBarrierType BarrierType,
        double BarrierLo,
        double BarrierHi,
        double Cash, // cash payoff for cash-or-nothing
        double S, // spot
        double Q, // dividend
        double R, // risk-free rate
        double T, // time to maturity
        double V, // volatility
        double Result, // expected result
        double Tol, // tolerance
        bool Moved); // the check moved to a divergence
}
