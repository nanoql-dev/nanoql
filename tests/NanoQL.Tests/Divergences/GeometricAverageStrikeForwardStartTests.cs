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
/// The geometric average-strike engine on a forward-starting average (fixed). QuantLib measures the fixing times from
/// the first fixing (<c>analytic_discr_geom_av_strike.cpp:57-101</c>), so it prices a forward-starting average as one
/// starting today and misses e^(−q t₁): on spot 100, q 3%, r 6%, σ 20% (Act/360), QuantLib's own case, a call on ten
/// fixings every 36 days from 36 days on, exercised at the last, prices 4.971092526326882, the value of the same
/// average started today (QuantLib's test expects 4.97109). nanoql measures every time from the evaluation date, in the
/// exact form, and prices 4.956201596311162. Checks: the forward start's value e^(−q t₁) times the value today on a
/// flat market, and an exact Monte Carlo.
/// </summary>
public class GeometricAverageStrikeForwardStartTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.03, A360), new FlatForward(Today, 0.06, A360), new BlackConstantVol(Today, new NullCalendar(), 0.20, A360));

    [Fact]
    public void TheForwardStartIsTheValueTodayCarriedByTheDividends()
    {
        double forward = Price(36);
        double today = Price(0);
        Assert.Equal(4.956201596311162, forward, 1e-12);
        Assert.Equal(today * Math.Exp(-0.03 * 36.0 / 360.0), forward, 1e-12 * forward);
    }

    [Fact]
    public void AnExactMonteCarloMeetsTheForwardStart()
    {
        (double mean, double error) = ExactMc(4000000);
        Assert.True(Math.Abs(Price(36) - mean) <= 3.0 * error, $"{Price(36)} against {mean} +- {error}");
        Assert.True(Math.Abs(4.971092526326882 - mean) > 4.0 * error, $"{mean} +- {error}");
    }

    private static double Price(int first)
    {
        Date[] dates = [.. Enumerable.Range(0, 10).Select(i => Today + first + (36 * i))];
        var option = new DiscreteAveragingAsianOption(
            AverageType.Geometric, 1.0, 0, dates, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(dates[^1]));
        return AnalyticDiscreteGeometricAverageStrikeAsianEngine.Calculate(option, Context, Process).NPV!.Value;
    }

    // the call on the spot at the last fixing less the geometric average of the ten, exact lognormal steps on QuantLib's
    // Mersenne twister and inverse normal, antithetic pairs
    private static (double Mean, double Error) ExactMc(int pairs)
    {
        double r = 0.06, q = 0.03, v = 0.20;
        double[] times = [.. Enumerable.Range(0, 10).Select(i => (36.0 + (36.0 * i)) / 360.0)];
        double[] steps = [.. times.Select((t, i) => i == 0 ? t : t - times[i - 1])];
        var rng = new MersenneTwisterUniformRng(42);
        var inv = new InverseCumulativeNormal();
        double discount = Math.Exp(-r * times[^1]);
        double sum = 0.0, sum2 = 0.0;
        var z = new double[steps.Length];
        for (int n = 0; n < pairs; n++)
        {
            for (int i = 0; i < z.Length; i++)
            {
                z[i] = inv.Evaluate(rng.Next().Value);
            }

            double pair = 0.0;
            foreach (double sign in (double[])[1.0, -1.0])
            {
                double ln = Math.Log(100.0), logSum = 0.0;
                for (int i = 0; i < steps.Length; i++)
                {
                    ln += ((r - q - (0.5 * v * v)) * steps[i]) + (v * Math.Sqrt(steps[i]) * sign * z[i]);
                    logSum += ln;
                }

                pair += 0.5 * Math.Max(Math.Exp(ln) - Math.Exp(logSum / times.Length), 0.0) * discount;
            }

            sum += pair;
            sum2 += pair * pair;
        }

        double mean = sum / pairs;
        return (mean, Math.Sqrt(((sum2 / pairs) - (mean * mean)) / pairs));
    }
}
