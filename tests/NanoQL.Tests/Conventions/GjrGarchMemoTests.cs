// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="AnalyticGJRGARCHEngine"/>'s moments' memo can't change an answer: QuantLib's engine keeps one maturity's
/// moments at a time and serves a later price at that maturity from them; nanoql's memo keeps each maturity's moments,
/// keyed by the day count and the rate they read and computed on the engine's immutable model, so a warm engine's price
/// is a fresh engine's bit for bit, in any order of strikes and maturities, for expiries sharing a day count on a
/// sloped curve, and on eight threads sharing the engine.
/// </summary>
public class GjrGarchMemoTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly GJRGARCHModel Model = new(new GJRGARCHProcess(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.0, A365), 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0));

    private static readonly int[] Days = [30, 90, 180, 365];
    private static readonly double[] Strikes = [35.0, 45.0, 50.0, 55.0, 60.0];

    [Fact]
    public void AWarmEnginesPriceIsAFreshEnginesInAnyOrder()
    {
        (int Days, double Strike, OptionType Type)[] options =
            [.. Days.SelectMany(d => Strikes.SelectMany(k => new[] { (d, k, OptionType.Call), (d, k, OptionType.Put) }))];
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var engine = new AnalyticGJRGARCHEngine(Model);
            var rng = new Random(seed);
            foreach ((int days, double strike, OptionType type) in options.OrderBy(_ => rng.Next()))
            {
                VanillaOption option = OptionOf(days, strike, type);
                Assert.Equal(AnalyticGJRGARCHEngine.Calculate(option, Context, Model).NPV, engine.Calculate(option, Context).NPV);
            }
        }
    }

    [Fact]
    public void EightThreadsSharingAnEngineGetFreshEnginesPrices()
    {
        (int Days, double Strike)[] options = [.. Days.SelectMany(d => Strikes.Select(k => (d, k)))];
        double?[] fresh = [.. options.Select(o => AnalyticGJRGARCHEngine.Calculate(OptionOf(o.Days, o.Strike, OptionType.Call), Context, Model).NPV)];
        var engine = new AnalyticGJRGARCHEngine(Model);
        double?[][] shared = new double?[8][];
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, t =>
        {
            shared[t] = new double?[options.Length];
            for (int k = 0; k < options.Length; ++k)
            {
                int i = (k + (t * 3)) % options.Length;
                shared[t][i] = engine.Calculate(OptionOf(options[i].Days, options[i].Strike, OptionType.Call), Context).NPV;
            }
        });

        foreach (double?[] prices in shared)
        {
            Assert.Equal(fresh, prices);
        }
    }

    [Fact]
    public void ExpiriesSharingADayCountOnASlopedCurveKeepTheirOwnMoments()
    {
        // at 252 days a year the expiries 30 and 31 calendar days out both run 21 days, their rates apart on the curve
        var model = new GJRGARCHModel(new GJRGARCHProcess(
            new ZeroCurve([Today, Today + 365], [0.02, 0.06], A365), new FlatForward(Today, 0.0, A365), 50.0, 2.0e-4, 2.0e-6,
            0.024, 0.93, 0.059, 0.1, 252.0));
        var engine = new AnalyticGJRGARCHEngine(model);
        _ = engine.Calculate(OptionOf(30, 50.0, OptionType.Call), Context);
        VanillaOption later = OptionOf(31, 50.0, OptionType.Call);
        Assert.Equal(AnalyticGJRGARCHEngine.Calculate(later, Context, model).NPV, engine.Calculate(later, Context).NPV);
    }

    private static VanillaOption OptionOf(int days, double strike, OptionType type) =>
        new(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + days));
}
