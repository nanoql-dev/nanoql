// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FD Heston-Hull-White engine's multiple strikes after a reference whose payoff isn't a plain vanilla (the FD
/// Heston engine's twin; <c>fdhestonhullwhitevanillaengine.cpp:78-95</c>, <c>:175-188</c>). QuantLib's cache stores the
/// reference payoff's scaled values V(S·d)/d under plain vanilla keys and serves them to later plain vanillas: after a
/// cash-or-nothing call (100, cash 10) the plain calls at 80, 100 and 120 are served 6.848, 5.968 and 1.616 where their
/// direct prices are 23.033, 8.076 and 1.106 (QuantLib 1.44). nanoql's <c>CalculateStrikes</c> solves each strike's
/// plain vanilla alone after such a reference, QuantLib's engine priced for that strike.
/// </summary>
public class FdHestonHullWhiteStrikesReferenceTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AfterANonPlainReferenceEachStrikeIsSolvedAlone()
    {
        YieldTermStructure r = new FlatForward(Today, 0.03, A365);
        var model = new HestonModel(new HestonProcess(r, new FlatForward(Today, 0.01, A365), 100.0, 0.04, 1.5, 0.04, 0.5, -0.6));
        var hullWhite = new HullWhiteProcess(r, 0.1, 0.01);
        var exercise = new EuropeanExercise(Today + 365);
        var reference = new VanillaOption(new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), exercise);
        double[] strikes = [80.0, 100.0, 120.0];
        double[] quantLibDirect = [23.033375851086941, 8.0755123897758665, 1.1060409872896042];

        ImmutableArray<OneAssetOptionResults> results = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            reference, strikes, Context, model, hullWhite, -0.5, 20, 40, 10, 7, 0, false);
        for (int i = 0; i < strikes.Length; ++i)
        {
            var plain = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strikes[i]), exercise);
            OneAssetOptionResults alone = FdHestonHullWhiteVanillaEngine.Calculate(plain, Context, model, hullWhite, -0.5, 20, 40, 10, 7, 0, false);
            Assert.Equal(alone.NPV, results[i].NPV);
            Assert.Equal(alone.Delta, results[i].Delta);
            Assert.True(Math.Abs(results[i].NPV!.Value - quantLibDirect[i]) <= 1e-10 * quantLibDirect[i], $"{strikes[i]}: {results[i].NPV}");
        }
    }
}
