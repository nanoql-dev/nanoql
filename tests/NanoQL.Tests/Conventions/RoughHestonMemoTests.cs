// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The rough Heston engine keeps QuantLib's caches inside itself: the characteristic function by (z, t), the lifted
/// grid by t and the Riemann-Liouville weights, each computed from the engine's immutable model and settings, so a warm
/// engine's answers are a fresh engine's bit for bit, in any order, and eight threads sharing one engine get one
/// thread's bits.
/// </summary>
public class RoughHestonMemoTests
{
    private static readonly Date Today = new(2, Month.July, 2026);

    [Fact]
    public void AWarmEnginesAnswersAreAFreshEnginesInAnyOrder()
    {
        foreach (AnalyticRoughHestonEngineApproximation route in Enum.GetValues<AnalyticRoughHestonEngineApproximation>())
        {
            RoughHestonModel model = Model();
            (PlainVanillaPayoff Payoff, double T)[] queries = Queries();

            double[] fresh = [.. queries.Select(q => new AnalyticRoughHestonEngine(model, 64, 64, route).PriceVanillaPayoff(q.Payoff, q.T))];

            var warm = new AnalyticRoughHestonEngine(model, 64, 64, route);
            int[] order = [5, 0, 3, 1, 4, 2, 5, 3, 0];
            foreach (int i in order)
            {
                Assert.Equal(fresh[i], warm.PriceVanillaPayoff(queries[i].Payoff, queries[i].T));
            }

            // the members read the same caches
            var z = new Complex(2.0, -1.5);
            Assert.Equal(new AnalyticRoughHestonEngine(model, 64, 64, route).ChF(z, 1.0), warm.ChF(z, 1.0));
            Assert.Equal(new AnalyticRoughHestonEngine(model, 64, 64, route).RiccatiSolution(z, 1.0), warm.RiccatiSolution(z, 1.0));
        }
    }

    [Fact]
    public void EightThreadsGetOneThreadsBits()
    {
        RoughHestonModel model = Model();
        (PlainVanillaPayoff Payoff, double T)[] queries = Queries();
        double[] expected = [.. queries.Select(q => new AnalyticRoughHestonEngine(model, 64, 64).PriceVanillaPayoff(q.Payoff, q.T))];

        foreach (AnalyticRoughHestonEngineApproximation route in new[]
                 {
                     AnalyticRoughHestonEngineApproximation.AdamsPredictorCorrector, AnalyticRoughHestonEngineApproximation.Lifted,
                 })
        {
            double[] single = [.. queries.Select(q => new AnalyticRoughHestonEngine(model, 64, 64, route).PriceVanillaPayoff(q.Payoff, q.T))];
            var shared = new AnalyticRoughHestonEngine(model, 64, 64, route);
            double[][] results = new double[8][];
            Parallel.For(0, 8, k =>
            {
                results[k] = [.. Enumerable.Range(0, queries.Length).Select(i => (i + k) % queries.Length)
                    .Select(i => (i, shared.PriceVanillaPayoff(queries[i].Payoff, queries[i].T)))
                    .OrderBy(p => p.i).Select(p => p.Item2)];
            });
            Assert.All(results, r => Assert.Equal(single, r));
        }

        Assert.Equal(expected, queries.Select(q => new AnalyticRoughHestonEngine(model, 64, 64).PriceVanillaPayoff(q.Payoff, q.T)).ToArray());
    }

    private static RoughHestonModel Model()
    {
        var rTS = new FlatForward(Today, 0.03, new Actual365Fixed());
        var qTS = new FlatForward(Today, 0.01, new Actual365Fixed());
        return new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.1);
    }

    private static (PlainVanillaPayoff Payoff, double T)[] Queries() =>
    [
        (new PlainVanillaPayoff(OptionType.Call, 100.0), 1.0),
        (new PlainVanillaPayoff(OptionType.Put, 90.0), 0.5),
        (new PlainVanillaPayoff(OptionType.Call, 120.0), 2.0),
        (new PlainVanillaPayoff(OptionType.Call, 80.0), 1.0),
        (new PlainVanillaPayoff(OptionType.Put, 110.0), 0.25),
        (new PlainVanillaPayoff(OptionType.Call, 100.0), 0.5),
    ];
}
