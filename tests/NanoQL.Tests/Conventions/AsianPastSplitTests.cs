// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Asian option built from every fixing date with the past fixings' values splits them at the context's evaluation
/// date, as QuantLib's <c>setupArguments</c> splits them at <c>Settings::evaluationDate()</c> (the S-bar plan's SD18):
/// a date before it is past, one on it to come. The option is one object; priced in two contexts it reads two splits,
/// each the option built from a running sum and a count of past fixings with the dates to come; and a context past more
/// dates than values were given is refused with QuantLib's text. The option keeps the values as given.
/// </summary>
public class AsianPastSplitTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly PlainVanillaPayoff Payoff = new(OptionType.Call, 100.0);
    private static readonly EuropeanExercise Exercise = new(Today + 365);
    private static readonly Date[] Dates = [Today - 30, Today, Today + 120, Today + 240, Today + 365];

    [Fact]
    public void ADateOnTheEvaluationDateIsToCome()
    {
        var option = new DiscreteAveragingAsianOption(AverageType.Arithmetic, Dates, Payoff, Exercise, [95.0, 102.0]);
        var seasoned = new DiscreteAveragingAsianOption(AverageType.Arithmetic, 95.0, 1, Dates[1..], Payoff, Exercise);
        var context = new EvaluationContext { EvaluationDate = Today };
        Assert.True(option.AllPastFixingsProvided);
        Assert.Equal([95.0, 102.0], option.AllPastFixings);
        Assert.Equal(
            TurnbullWakemanAsianEngine.Calculate(seasoned, context, Process).NPV,
            TurnbullWakemanAsianEngine.Calculate(option, context, Process).NPV);
    }

    [Fact]
    public void TheSameOptionReadsEachContextsSplit()
    {
        var option = new DiscreteAveragingAsianOption(AverageType.Geometric, Dates, Payoff, Exercise, [95.0, 102.0]);
        var later = new EvaluationContext { EvaluationDate = Today + 1 };
        var seasoned = new DiscreteAveragingAsianOption(AverageType.Geometric, 95.0 * 102.0, 2, Dates[2..], Payoff, Exercise);
        Assert.Equal(
            AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(seasoned, later, Process).NPV,
            AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option, later, Process).NPV);
    }

    [Fact]
    public void TooFewPastValuesAreRefused()
    {
        var option = new DiscreteAveragingAsianOption(AverageType.Arithmetic, Dates, Payoff, Exercise, [95.0]);
        var later = new EvaluationContext { EvaluationDate = Today + 1 };
        NanoQLException e = Assert.Throws<NanoQLException>(() => TurnbullWakemanAsianEngine.Calculate(option, later, Process));
        Assert.Equal("Not enough past fixings have been provided for the required historical fixing dates", e.Message);
    }
}
