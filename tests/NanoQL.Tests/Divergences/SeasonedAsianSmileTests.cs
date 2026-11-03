// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The seasoned arithmetic Asians on a smile (fixed). With f future fixings of m, an accrued sum A and the strike K,
/// max(avg − K, 0) = (f/m)·max(avg_f − K_f, 0) with K_f = (K − A/m)·m/f, so a seasoned price is exactly (f/m) times the
/// unseasoned price at K_f. QuantLib's Turnbull-Wakeman engine reads its variances at K − A/m and its Choi engine its
/// correlations at K − A/m and its volatilities at K (<c>turnbullwakemanasianengine.cpp:94-95</c>,
/// <c>choiasianengine.cpp:104-122</c>), so on a smile whose skew decays with maturity (0.22 + skewⱼ·(100 − K)/100,
/// skews 0.30, 0.18 and 0.08 at 60, 120 and 240 days; spot 100, r 5%, q 0) they break the identity, Turnbull-Wakeman by
/// +42% and +1,302% and Choi by −2.6% to +306% on six past fixings and six monthly ones to come. nanoql reads both at
/// the future average's own strike K_f. Check: the identity, against each engine's own unseasoned value at K_f.
/// </summary>
public class SeasonedAsianSmileTests
{
    private static readonly Date Today = new(1, Month.January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Date[] Future = [.. Enumerable.Range(1, 6).Select(i => Today + (30 * i))];
    private static readonly (double Sum, double Strike)[] Cases = [(6 * 80.0, 100.0), (6 * 120.0, 110.0), (6 * 90.0, 95.0)];

    [Fact]
    public void TurnbullWakemanKeepsTheSeasoningIdentity() =>
        Check((option, p) => TurnbullWakemanAsianEngine.Calculate(option, Context, p).NPV!.Value);

    [Fact]
    public void ChoiKeepsTheSeasoningIdentity() =>
        Check((option, p) => ChoiAsianEngine.Calculate(option, Context, p).NPV!.Value);

    private static void Check(Func<DiscreteAveragingAsianOption, GeneralizedBlackScholesProcess, double> price)
    {
        GeneralizedBlackScholesProcess p = Smile();
        foreach ((double sum, double strike) in Cases)
        {
            double futureStrike = (strike - (sum / 12.0)) * 12.0 / 6.0;
            var seasoned = new DiscreteAveragingAsianOption(
                AverageType.Arithmetic, sum, 6, Future, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Future[^1]));
            var unseasoned = new DiscreteAveragingAsianOption(
                AverageType.Arithmetic, 0.0, 0, Future, new PlainVanillaPayoff(OptionType.Call, futureStrike), new EuropeanExercise(Future[^1]));
            double expected = price(unseasoned, p) * 6.0 / 12.0;
            Assert.Equal(expected, price(seasoned, p), 1e-12 * expected);
        }
    }

    private static GeneralizedBlackScholesProcess Smile()
    {
        DayCounter a365 = new Actual365Fixed();
        double[] strikes = [40.0, 60.0, 80.0, 100.0, 120.0, 140.0, 160.0];
        double[] skews = [0.30, 0.18, 0.08];
        var vols = new Matrix(strikes.Length, skews.Length);
        for (int i = 0; i < strikes.Length; i++)
        {
            for (int j = 0; j < skews.Length; j++)
            {
                vols[i, j] = 0.22 + (skews[j] * (100.0 - strikes[i]) / 100.0);
            }
        }

        BlackVarianceSurface surface = new BlackVarianceSurface(Today, null, [Today + 60, Today + 120, Today + 240], strikes, vols, a365).EnableExtrapolation();
        return new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.0, a365), new FlatForward(Today, 0.05, a365), surface);
    }
}
