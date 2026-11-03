// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The GJR-GARCH engines on a risk-free curve dated after the evaluation date. QuantLib's engines run from the
/// risk-free curve's reference date (<c>process-&gt;time</c>): their days to the maturity are short by the curve's
/// settlement, and an option expiring before the curve's date is refused ("negative time given"). nanoql's run on the
/// process's model clock, from the earliest reference date of its curves, each curve accruing nothing before its own
/// date. QuantLib's check: its engines on the market restated on one clock, the risk-free discount 1 to its date and 5%
/// after (a log-linear discount curve), which nanoql's engines on the original market reproduce to rounding
/// (QuantLib-Python 1.44 and QuantLib 1.44).
/// </summary>
public class GjrGarchMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    // the gjrgarchmodel test's set at lambda 0.1 from its stationary variance, the risk-free curve settling T+2
    private static readonly GJRGARCHProcess Process = new(
        new FlatForward(Today + 2, 0.05, A365), new FlatForward(Today, 0.0, A365), 50.0, 0.00017778348033623922, 2.0e-6, 0.024, 0.93,
        0.059, 0.1, 365.0);

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 50.0), new EuropeanExercise(Today + 90));
        AssertClose(2.0758096873774576, AnalyticGJRGARCHEngine.Calculate(put, Context, new GJRGARCHModel(Process)).NPV!.Value); // as given 2.051730917050641
        double mc = MCEuropeanGJRGARCHEngine.Calculate<PseudoRandom, GeneralStatistics>(put, Context, Process, 30, null, false, 2000, null, null, 42)
            .NPV!.Value;
        AssertClose(2.1136364289567027, mc); // as given 2.088199450259117
    }

    [Fact]
    public void AnOptionExpiringBeforeTheRiskFreeCurvesDatePrices()
    {
        // QuantLib: "negative time (-0.00273973) given"
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 50.0), new EuropeanExercise(Today + 1));
        AssertClose(0.2659667699916213, AnalyticGJRGARCHEngine.Calculate(put, Context, new GJRGARCHModel(Process)).NPV!.Value);
    }

    private static void AssertClose(double expected, double calculated) =>
        Assert.True(
            Math.Abs(calculated - expected) <= 1e-11 * Math.Abs(expected),
            $"expected {expected}, calculated {calculated}");
}
