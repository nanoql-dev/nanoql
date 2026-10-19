// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A swaption given no swap or no exercise is refused on entry, with <see cref="ArgumentNullException"/> naming the
/// parameter. QuantLib's constructor takes both unchecked and dereferences them before its arguments' checks ("swap not
/// set", "exercise not set", <c>swaption.cpp:177-178</c>) could refuse them: results C++ leaves undefined, where the
/// wheel stops on Boost's assertion.
/// </summary>
public class SwaptionNullArgumentTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    /// <summary>
    /// No swap: QuantLib's constructor calls <c>swap_-&gt;alwaysForwardNotifications()</c> on it
    /// (<c>swaption.cpp:137-148</c>), a null dereference.
    /// </summary>
    [Fact]
    public void ASwaptionWithoutASwapIsRefused()
    {
        var exercise = new EuropeanExercise(new Date(15, Month.January, 2026));
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => new Swaption(null!, exercise));
        Assert.Equal("swap", e.ParamName);

        // With neither, the swap comes first, in the parameters' order.
        Assert.Equal("swap", Assert.Throws<ArgumentNullException>(() => new Swaption(null!, null!)).ParamName);
    }

    /// <summary>
    /// No exercise: QuantLib's <c>isExpired</c> reads <c>exercise_-&gt;dates()</c> (<c>swaption.cpp:159</c>), which
    /// <c>Instrument::calculate</c> runs before the arguments' "exercise not set" check, a null dereference when the
    /// swaption is priced.
    /// </summary>
    [Fact]
    public void ASwaptionWithoutAnExerciseIsRefused()
    {
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithForwardStart(new Period(1, TimeUnit.Years))
            .WithFixedRate(0.03)
            .ToVanillaSwap(Context);
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => new Swaption(swap, null!));
        Assert.Equal("exercise", e.ParamName);
    }
}
