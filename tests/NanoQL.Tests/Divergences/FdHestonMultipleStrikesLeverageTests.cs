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
/// The multiple-strikes calculation with a leverage function (the S-hes plan's HR7 (i)). QuantLib's cache serves
/// each strike V(S·d)/d from one solve (<c>fdhestonvanillaengine.cpp:209-224</c>), which assumes the model scales with
/// the spot; a spot-dependent leverage breaks it: with a leverage of 1.4, 1.0 and 0.7 at strikes 50, 100 and 150, after a
/// call at 100 it serves 50.50657855051417, 27.518542374323449, 0.8299478410251605, 0.057935950793473072 and
/// 0.0010848235476194543 at 50, 75, 125, 150 and 200 where its engine priced for each strike alone gives
/// 50.704063145305852, 28.070741991049783, 0.43296316837484017, 0.0063957853450491283 and 2.489998860627156e-05
/// [executed: <c>scratchpad\cxxprobe\src\c1oracle.cpp</c>]. nanoql's <c>CalculateStrikes</c> solves each strike alone
/// there. Check: QuantLib's engine priced for each strike alone.
/// </summary>
public class FdHestonMultipleStrikesLeverageTests
{
    private static readonly Date Today = new(15, March, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly double[] Strikes = [50, 75, 100, 125, 150, 200];

    private static readonly HestonModel Model = new(new HestonProcess(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.5, -0.7));

    [Fact]
    public void EachStrikeIsTheEnginesPriceForItAlone()
    {
        ImmutableArray<OneAssetOptionResults> results = FdHestonVanillaEngine.CalculateStrikes(
            Option(100.0), Strikes, Context, Model, 100, 200, 100, 0, null, Leverage());
        for (int i = 0; i < Strikes.Length; ++i)
        {
            OneAssetOptionResults alone = FdHestonVanillaEngine.Calculate(
                Option(Strikes[i]), Context, Model, 100, 200, 100, 0, null, Leverage());
            Assert.Equal(alone.NPV, results[i].NPV);
            Assert.Equal(alone.Delta, results[i].Delta);
            Assert.Equal(alone.Gamma, results[i].Gamma);
            Assert.Equal(alone.Theta, results[i].Theta);
        }
    }

    [Fact]
    public void EachStrikeIsQuantLibsEnginePricedForItAlone()
    {
        double[] quantLib =
        [
            50.704063145305852, 28.070741991049783, 8.7674936942583859, 0.43296316837484017, 0.0063957853450491283,
            2.489998860627156e-05,
        ];
        ImmutableArray<OneAssetOptionResults> results = FdHestonVanillaEngine.CalculateStrikes(
            Option(100.0), Strikes, Context, Model, 100, 200, 100, 0, null, Leverage());
        for (int i = 0; i < Strikes.Length; ++i)
        {
            double npv = results[i].NPV!.Value;
            Assert.True(
                Math.Abs(npv - quantLib[i]) <= 1e-10 * Math.Max(quantLib[i], 1.0),
                $"K {Strikes[i]}: {npv} against QuantLib's {quantLib[i]}");
        }
    }

    private static VanillaOption Option(double strike) =>
        new(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + 365));

    // 1.4, 1.0 and 0.7 at strikes 50, 100 and 150, at times 0, 0.3, 0.7 and 2
    private static FixedLocalVolSurface Leverage()
    {
        var values = new NanoQL.Numerics.Matrix(3, 4);
        for (int j = 0; j < 4; ++j)
        {
            values[0, j] = 1.4;
            values[1, j] = 1.0;
            values[2, j] = 0.7;
        }

        return new FixedLocalVolSurface(Today, new[] { 0.0, 0.3, 0.7, 2.0 }, new[] { 50.0, 100.0, 150.0 }, values, A365);
    }
}
