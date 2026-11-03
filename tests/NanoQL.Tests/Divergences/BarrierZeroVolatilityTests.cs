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
/// The analytic barrier engine at zero volatility (taken from QuantLib's master ahead of the pin). QuantLib 1.44
/// divides by the zero standard deviation (<c>analyticbarrierengine.cpp:181-238</c>): a one-year up-and-out put at 107,
/// strike 105, rebate 4, prices NaN (QuantLib-Python 1.44). QuantLib's master fixed it after the pin (#2853,
/// <c>e29c64126</c>, merged as <c>2768fafdc</c> on 2026-10-14): with a variance below machine epsilon the underlying
/// follows its forward, a spot on the barrier counts as a hit, a knock-in pays the vanilla payoff or the rebate at
/// maturity, and a knock-out its rebate at the hit or the vanilla payoff. This is master's <c>testZeroVolatility</c>,
/// until the pin moves to a release that carries it.
/// </summary>
public class BarrierZeroVolatilityTests
{
    [Fact]
    public void TestZeroVolatility()
    {
        // Testing barrier options with zero volatility...

        DayCounter dc = new Actual365Fixed();

        var today = new Date(11, February, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturity = today + new Period(1, TimeUnit.Years);

        double spot = 100.0;
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.0, dc);

        void Check(
            double strike,
            OptionType optionType,
            double barrier,
            BarrierType barrierType,
            double rebate,
            double r,
            double q,
            double expected)
        {
            var process = new BlackScholesMertonProcess(
                spot, new FlatForward(today, q, dc), new FlatForward(today, r, dc), volTS);
            var option = new BarrierOption(
                barrierType, barrier, rebate, new PlainVanillaPayoff(
                    optionType, strike), new EuropeanExercise(maturity));
            double value = AnalyticBarrierEngine.Calculate(option, context, process).NPV!.Value;
            double diff = Math.Abs(value - expected);
            Assert.True(
                diff <= 1.0e-10,
                $"Failed to match expected price: strike {strike}, option type {optionType}, barrier {barrier}, "
                + $"barrier type {barrierType}, rebate {rebate}, risk-free rate {r}, dividend yield {q}, "
                + $"expected {expected}, calculated {value}");
        }

        // the underlying follows its forward; a knock-out rebate is paid when the forward
        // reaches the barrier, a knock-in rebate at maturity

        // strike, type, barrier, barrier type, rebate, r, q, expected

        Check(105.0, OptionType.Put, 107.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 2.891797647676558); // not hit
        Check(105.0, OptionType.Put, 101.0, BarrierType.UpOut, 4.0, 0.03, 0.01, 3.940741347366294); // hit at 0.4975
        Check(100.0, OptionType.Call, 101.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 2.955446645149196); // hit
        Check(105.0, OptionType.Call, 107.0, BarrierType.UpIn, 4.0, 0.03, 0.0, 3.881782134194033); // not hit
        Check(91.0, OptionType.Call, 93.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 6.950018483676519); // not hit
        Check(101.0, OptionType.Put, 99.0, BarrierType.DownOut, 4.0, 0.01, 0.03, 3.979949748426480); // hit at 0.5025
        Check(100.0, OptionType.Put, 99.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 2.926039459684483); // hit
        Check(91.0, OptionType.Call, 93.0, BarrierType.DownIn, 4.0, 0.01, 0.04, 3.960199334996672); // not hit
        Check(95.0, OptionType.Call, 100.0, BarrierType.DownOut, 4.0, 0.03, 0.0, 4.0); // spot on the barrier
    }
}
