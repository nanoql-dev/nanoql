// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the swing exercise's and the swing option's refusals, with
/// QuantLib's texts. The exercise's date times are its constructor's arguments, refused there as
/// <see cref="ArgumentException"/>s naming them; the option's rights are checked by <see cref="VanillaSwingOption.Validate"/>,
/// as QuantLib's arguments' <c>validate</c> checks them at pricing, a <see cref="NanoQLException"/>; a payoff that
/// isn't striked is QuantLib's "no payoff given" there, its arguments holding the striked cast.
/// </summary>
public class SwingOptionErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void ExerciseRefusals()
    {
        Refused<ArgumentException>(
            () => new SwingExercise([Today + 1, Today + 2], [0]), "seconds", "dates and seconds must have the same size");
        Refused<ArgumentOutOfRangeException>(
            () => new SwingExercise([Today + 1], [86400]), "seconds", "a date can not have more than 24*3600 seconds");
        Refused<ArgumentOutOfRangeException>(
            () => new SwingExercise([Today + 1], [-1]), "seconds", "a date can not have more than 24*3600 seconds");
        Refused<ArgumentException>(
            () => new SwingExercise([Today + 1, Today + 1], [600, 600]), "dates", "date times must be sorted");
        Refused<ArgumentException>(() => new SwingExercise([]), "dates", "no exercise date given");
        Refused<ArgumentOutOfRangeException>(() => new SwingExercise(Today + 1, Today + 2, 0), "stepSizeSecs", null);
        Refused<ArgumentException>(
            () => new SwingExercise([Today + 1]).ExerciseTimes(new Actual365Fixed(), Today + 2),
            "refDate",
            "exercise dates must not contain past date");
    }

    [Fact]
    public void OptionRefusals()
    {
        var exercise = new SwingExercise([Today + 30, Today + 60]);
        var forward = new VanillaForwardPayoff(OptionType.Put, 100.0);
        Refused<ArgumentOutOfRangeException>(() => new VanillaSwingOption(forward, exercise, -1, 1), "minExerciseRights", null);
        Refused<ArgumentNullException>(() => new VanillaSwingOption(forward, null!, 0, 1), "ex", null);
        Refused<NanoQLException>(() => Price(new VanillaSwingOption(forward, exercise, 2, 1)), null, "minExerciseRights <= maxExerciseRights");
        Refused<NanoQLException>(
            () => Price(new VanillaSwingOption(forward, exercise, 0, 3)), null, "number of exercise rights exceeds number of exercise dates");
        Refused<NanoQLException>(() => Price(new VanillaSwingOption(new Capped(), exercise, 0, 1)), null, "no payoff given");
    }

    private static void Price(VanillaSwingOption option)
    {
        var dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, dc), new FlatForward(Today, 0.03, dc), new BlackConstantVol(Today, new NullCalendar(), 0.2, dc));
        FdSimpleBSSwingEngine.Calculate(option, new EvaluationContext { EvaluationDate = Today }, process);
    }

    private static void Refused<TException>(Action call, string? paramName, string? message)
        where TException : Exception
    {
        TException e = Assert.Throws<TException>(call);
        if (e is ArgumentException a)
        {
            Assert.Equal(paramName, a.ParamName);
        }

        if (message is not null)
        {
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }
    }

    // a payoff that isn't striked, testFdEnginesWithNonStrikedPayoff's capped call
    private sealed class Capped : Payoff
    {
        public override string Name => "CappedCall";

        public override string Description => Name;

        public override double Evaluate(double price) => Math.Min(Math.Max(price - 100.0, 0.0), 20.0);
    }
}
