// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo digital engine on an exercise window (the S-van plan's VR4 (iii), under R8). QuantLib's analytic
/// engine refuses a window ("American option with window exercise not handled yet",
/// <c>analyticdigitalamericanengine.cpp:41-43</c>), and its Monte Carlo engine prices from today: an up-and-in call at
/// 110 paying 10 (S 100, r 5%, vol 25%, one year; 365 steps, 20,000 samples, seed 1) gives 7.164398475241645 with the
/// window opening today and in 180 days alike [executed on the wheel: <c>oracle_c1.py</c>]. nanoql's engine puts the
/// window's opening on the grid and counts crossings from it, a path already past the strike there paid then.
/// QuantLib's check: its own closed form, the cash at the hit from the opening (<see cref="AmericanPayoffAtHit"/>),
/// integrated over the lognormal density of the underlying at the opening and discounted to today.
/// </summary>
public class McDigitalWindowTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly CashOrNothingPayoff Payoff = new(OptionType.Call, 110.0, 10.0);

    [Fact]
    public void AWindowCountsCrossingsFromItsOpening()
    {
        OneAssetOptionResults mc = Price(Today + 180);

        // QuantLib's closed form from the opening, over S(t1)'s lognormal density: z on [-8, 8], trapezoids
        double t1 = 180.0 / 365.0, tau = 1.0 - t1, r = 0.05, vol = 0.25;
        const int Nodes = 4001;
        double h = 16.0 / (Nodes - 1);
        double sum = 0.0;
        for (int i = 0; i < Nodes; i++)
        {
            double z = -8.0 + (i * h);
            double s1 = 100.0 * Math.Exp(((r - (0.5 * vol * vol)) * t1) + (vol * Math.Sqrt(t1) * z));
            double value = s1 >= Payoff.Strike
                ? Payoff.CashPayoff
                : new AmericanPayoffAtHit(s1, Math.Exp(-r * tau), 1.0, vol * vol * tau, Payoff).Value();
            double weight = i == 0 || i == Nodes - 1 ? 0.5 : 1.0;
            sum += weight * value * Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI);
        }

        double expected = Math.Exp(-r * t1) * sum * h;
        Assert.True(
            Math.Abs(mc.NPV!.Value - expected) <= 3.0 * mc.ErrorEstimate!.Value,
            $"{mc.NPV} ± {mc.ErrorEstimate} against {expected}");
    }

    [Fact]
    public void AWindowOpenTodayIsQuantLibs()
    {
        Assert.Equal(7.164398475241645, Price(Today).NPV);
    }

    private static OneAssetOptionResults Price(Date earliest) => MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(
        new VanillaOption(Payoff, new AmericanExercise(earliest, Today + 365)),
        Context,
        Process,
        365,
        null,
        false,
        false,
        20000,
        null,
        null,
        1);
}
