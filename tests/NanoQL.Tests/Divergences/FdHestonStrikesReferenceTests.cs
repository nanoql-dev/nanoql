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
/// The multiple-strikes calculation on a reference that isn't a plain vanilla. QuantLib's cache stores the reference
/// payoff's values scaled, V(S·d)/d, under plain-vanilla keys at each strike (<c>fdhestonvanillaengine.cpp:209-224</c>)
/// and serves them to a later plain vanilla (<c>:174-191</c>); V(S·d)/d is a vanilla at Kᵢ only for a plain-vanilla
/// reference. After a cash-or-nothing reference (100, cash 10) it serves plain calls at 80, 100 and 120
/// 6.8209430073262878, 5.8085085805369312 and 1.6286553941678532, after an asset-or-nothing one 91.147695950275022,
/// 66.230378622429299 and 17.487133347226365, where its engine priced for each strike alone gives 22.93727546246711,
/// 8.144976787928341 and 1.2010639430197885 (QuantLib 1.44). nanoql's <c>CalculateStrikes</c> solves each strike's
/// plain vanilla alone there. Check: QuantLib's engine priced for each strike alone.
/// </summary>
public class FdHestonStrikesReferenceTests
{
    private static readonly Date Today = new(15, March, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly double[] Strikes = [80.0, 100.0, 120.0];
    private static readonly double[] QuantLibDirect = [22.93727546246711, 8.144976787928341, 1.2010639430197885];

    private static readonly HestonModel Model = new(new HestonProcess(
        new FlatForward(Today, 0.03, A365), new FlatForward(Today, 0.01, A365), 100.0, 0.04, 1.5, 0.04, 0.5, -0.6));

    [Fact]
    public void ACashOrNothingReferenceGetsEachStrikeSolvedAlone() =>
        AssertEachStrikeAlone(new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0));

    [Fact]
    public void AnAssetOrNothingReferenceGetsEachStrikeSolvedAlone() =>
        AssertEachStrikeAlone(new AssetOrNothingPayoff(OptionType.Call, 100.0));

    private static void AssertEachStrikeAlone(StrikedTypePayoff reference)
    {
        var exercise = new EuropeanExercise(Today + 365);
        ImmutableArray<OneAssetOptionResults> results = FdHestonVanillaEngine.CalculateStrikes(
            new VanillaOption(reference, exercise), Strikes, Context, Model, 100, 200, 100);
        for (int i = 0; i < Strikes.Length; ++i)
        {
            OneAssetOptionResults alone = FdHestonVanillaEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, Strikes[i]), exercise), Context, Model, 100, 200, 100);
            Assert.Equal(alone.NPV, results[i].NPV);
            Assert.Equal(alone.Delta, results[i].Delta);
            double npv = results[i].NPV!.Value;
            Assert.True(
                Math.Abs(npv - QuantLibDirect[i]) <= 1e-10 * QuantLibDirect[i],
                $"K {Strikes[i]}: {npv} against QuantLib's {QuantLibDirect[i]}");
        }
    }
}
