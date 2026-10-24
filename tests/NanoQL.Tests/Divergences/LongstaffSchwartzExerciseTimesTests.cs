// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Longstaff-Schwartz pricer's exercise times (the S-van plan's VR4 (xxiv), under R8). QuantLib's pricer exercises
/// at every time of its grid (<c>longstaffschwartzpathpricer.hpp:111-133</c>, <c>:143-200</c>), while its engine adds
/// steps between a Bermudan option's dates and never puts an American option's earliest date on the grid
/// (<c>mclongstaffschwartzengine.hpp:218-225</c>): a Bermudan on more steps than dates, and an American option whose
/// window opens later, price as an American exercisable throughout. nanoql's pricer exercises only at the grid's
/// exercise times. QuantLib's checks: its finite-difference engine (800 × 800) on the Bermudan and the window, within
/// the Monte Carlo error; and on one step per date, QuantLib's own bits [executed on the wheel: <c>oracle_c1.py</c>].
/// </summary>
public class LongstaffSchwartzExerciseTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        80.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly Date[] Quarters = [Today + 91, Today + 182, Today + 273, Today + 365];

    [Fact]
    public void ABermudanOrAWindowExercisesOnlyAtItsExerciseTimes()
    {
        // a quarterly Bermudan put, S 80, K 100, on 50 steps: QuantLib's FD 19.950668936334548, QuantLib's LS
        // 20.31917623834806 ± 0.0562, 6.5 errors above
        (double bermudan, double bermudanError) = Price(new BermudanExercise(Quarters), 50);
        Assert.True(Math.Abs(bermudan - 19.950668936334548) <= 3.0 * bermudanError, $"{bermudan} ± {bermudanError}");

        // exercisable in its last three months: QuantLib's FD 18.996629766885693, QuantLib's LS 20.240342676162765
        (double window, double windowError) = Price(new AmericanExercise(Today + 273, Today + 365), 50);
        Assert.True(Math.Abs(window - 18.996629766885693) <= 3.0 * windowError, $"{window} ± {windowError}");
    }

    [Fact]
    public void OnOneStepPerDateTheBitsAreQuantLibs()
    {
        Assert.Equal(19.954065549362245, Price(new BermudanExercise(Quarters), 4).Value);
    }

    private static (double Value, double Error) Price(Exercise exercise, int steps)
    {
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), exercise);
        OneAssetOptionResults results = MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            option, Context, Process, steps, null, false, false, 20000, null, null, 42, 3, LsmBasisSystemPolynomialType.Monomial);
        return (results.NPV!.Value, results.ErrorEstimate!.Value);
    }
}
