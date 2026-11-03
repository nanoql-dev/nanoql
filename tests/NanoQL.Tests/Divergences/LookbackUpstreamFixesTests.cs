// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to the lookback family after the pin, which nanoql takes, with the master's own case.
/// #2856 (<c>9c44b7345</c>): the full fixed and floating lookbacks at zero carry, where 1.44's formulas are 0/0, take
/// their limit; the master switches to it below |λ| of 1e-7, nanoql at zero carry alone, the formula in double-double
/// around it (<c>LookbackSmallCarryTests</c>).
/// </summary>
public class LookbackUpstreamFixesTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestAnalyticContinuousLookbackZeroCarry()
    {
        // Upstream: QuantLib's master's case for #2856 (lookbackoptions.cpp's testAnalyticContinuousLookbackZeroCarry)
        // with r equal to q the formulas are 0/0; the expected values are their limit, evaluated in 50-digit arithmetic
        (OptionType Type, double Minmax, double Result)[] floatingValues =
        [
            (OptionType.Call, 90.0, 21.817075601551579),
            (OptionType.Put, 110.0, 26.407720137984153),
        ];

        (OptionType Type, double Strike, double Minmax, double Result)[] fixedValues =
        [
            (OptionType.Call, 95.0, 110.0, 31.163867260487723),
            (OptionType.Call, 105.0, 100.0, 20.604933499041011),
            (OptionType.Put, 105.0, 90.0, 26.573222724055149),
            (OptionType.Put, 95.0, 100.0, 16.226584162874658),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.05, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.30, dc));
        var exercise = new EuropeanExercise(today + TimeToDays(1.0));
        const double tol = 1.0e-8;

        foreach ((OptionType type, double minmax, double result) in floatingValues)
        {
            var option = new ContinuousFloatingLookbackOption(minmax, new FloatingTypePayoff(type), exercise);
            double calculated = AnalyticContinuousFloatingLookbackEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            Assert.True(Math.Abs(calculated - result) <= tol, $"floating {type} at {minmax}: expected {result}, calculated {calculated}");
        }

        foreach ((OptionType type, double strike, double minmax, double result) in fixedValues)
        {
            var option = new ContinuousFixedLookbackOption(minmax, new PlainVanillaPayoff(type, strike), exercise);
            double calculated = AnalyticContinuousFixedLookbackEngine.Calculate(option, RunnerContext, process).NPV!.Value;
            Assert.True(Math.Abs(calculated - result) <= tol, $"fixed {type} at {strike}, {minmax}: expected {result}, calculated {calculated}");
        }
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);
}
