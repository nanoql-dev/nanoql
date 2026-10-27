// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model specs, as tools/cxx/src/models/marketmodels/marketmodelspec.hpp reads them:
/// matrices, correlations, models and Brownian generator factories; and the reads the harness's units return, checked
/// against nanoql's (the curve states' and the models').
/// </summary>
internal static class MarketModelCxxInputs
{
    /// <summary>The reals of a JSON array.</summary>
    /// <param name="e">The array.</param>
    /// <returns>The values.</returns>
    public static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];

    /// <summary>The integers of a JSON array.</summary>
    /// <param name="e">The array.</param>
    /// <returns>The values.</returns>
    public static int[] Integers(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetInt32())];

    /// <summary>The matrix of a JSON array of rows.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>A new matrix.</returns>
    public static Matrix MatrixOf(JsonElement rows)
    {
        double[][] r = [.. rows.EnumerateArray().Select(Reals)];
        int columns = r.Length == 0 ? 0 : r[0].Length;
        var m = new Matrix(r.Length, columns);
        for (int i = 0; i < r.Length; ++i)
        {
            for (int j = 0; j < columns; ++j)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }

    /// <summary>A matrix's rows, as the vectors write them.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The rows.</returns>
    public static double[][] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => m.Row(i).ToArray())];

    /// <summary>The correlation a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The correlation.</returns>
    public static PiecewiseConstantCorrelation CorrelationOf(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "Exponential" => new ExponentialForwardCorrelation(
            Reals(spec.GetProperty("rateTimes")),
            spec.GetProperty("longTermCorr").GetDouble(),
            spec.GetProperty("beta").GetDouble(),
            spec.GetProperty("gamma").GetDouble(),
            Reals(spec.GetProperty("times"))),
        "TimeHomogeneous" => new TimeHomogeneousForwardCorrelation(
            MatrixOf(spec.GetProperty("fwdCorrelation")), Reals(spec.GetProperty("rateTimes"))),
        string t => throw new ArgumentException($"unknown correlation type {t}", nameof(spec)),
        null => throw new ArgumentException("a correlation spec needs a type", nameof(spec)),
    };

    /// <summary>The model a spec describes, FlatVol or AbcdVol, on the evolution of its rate and evolution
    /// times.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The model.</returns>
    public static MarketModel ModelOf(JsonElement spec)
    {
        var evolution = new EvolutionDescription(Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("evolutionTimes")));
        if (spec.GetProperty("type").GetString() == "AbcdVol")
        {
            double[] abcd = Reals(spec.GetProperty("abcd"));
            return new AbcdVol(
                abcd[0],
                abcd[1],
                abcd[2],
                abcd[3],
                Reals(spec.GetProperty("ks")),
                CorrelationOf(spec.GetProperty("corr")),
                evolution,
                spec.GetProperty("factors").GetInt32(),
                Reals(spec.GetProperty("initialRates")),
                Reals(spec.GetProperty("displacements")));
        }

        Assert.Equal("FlatVol", spec.GetProperty("type").GetString());
        return new FlatVol(
            Reals(spec.GetProperty("vols")),
            CorrelationOf(spec.GetProperty("corr")),
            evolution,
            spec.GetProperty("factors").GetInt32(),
            Reals(spec.GetProperty("initialRates")),
            Reals(spec.GetProperty("displacements")));
    }

    /// <summary>The Brownian generator factory a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The factory.</returns>
    public static BrownianGeneratorFactory FactoryOf(JsonElement spec)
    {
        long seed = spec.GetProperty("seed").GetInt64();
        return spec.GetProperty("type").GetString() switch
        {
            "MT" => new MTBrownianGeneratorFactory(seed),
            "Sobol" => new SobolBrownianGeneratorFactory((SobolBrownianGeneratorBaseOrdering)spec.GetProperty("ordering").GetInt32(), seed),
            "Burley" => BurleyFactoryOf(spec),
            string t => throw new ArgumentException($"unknown generator type {t}", nameof(spec)),
            null => throw new ArgumentException("a generator spec needs a type", nameof(spec)),
        };
    }

    /// <summary>The Burley-scrambled Sobol factory a spec of the harness's "Burley" type describes, on Jaeckel's
    /// integers.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The factory.</returns>
    public static BrownianGeneratorFactory BurleyFactoryOf(JsonElement spec) =>
        new Burley2020SobolBrownianGeneratorFactory(
            (SobolBrownianGeneratorBaseOrdering)spec.GetProperty("ordering").GetInt32(),
            spec.GetProperty("seed").GetInt64(),
            SobolRsgDirectionIntegers.Jaeckel,
            spec.GetProperty("scrambleSeed").GetInt64());

    /// <summary>The square-root volatility process a spec describes.</summary>
    /// <param name="spec">The spec: the harness's SquareRootAndersen object.</param>
    /// <returns>The process.</returns>
    public static SquareRootAndersen SquareRootAndersenOf(JsonElement spec) =>
        new(
            spec.GetProperty("meanLevel").GetDouble(),
            spec.GetProperty("reversionSpeed").GetDouble(),
            spec.GetProperty("volVar").GetDouble(),
            spec.GetProperty("v0").GetDouble(),
            Reals(spec.GetProperty("evolutionTimes")),
            spec.GetProperty("numberSubSteps").GetInt32(),
            spec.GetProperty("w1").GetDouble(),
            spec.GetProperty("w2").GetDouble(),
            spec.GetProperty("cutPoint").GetDouble());

    /// <summary>Checks a model's reads, as the harness's modelResults writes them.</summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="model">The model, or a function building it, whose throw a case records.</param>
    public static void CheckModel(VectorAssert check, VectorCase c, Func<MarketModel> model)
    {
        var m = new Lazy<MarketModel>(model);
        check.Output(c, "numberOfRates", Lazy(() => m.Value.NumberOfRates));
        check.Output(c, "numberOfFactors", Lazy(() => m.Value.NumberOfFactors));
        check.Output(c, "numberOfSteps", Lazy(() => m.Value.NumberOfSteps));
        check.Output(c, "initialRates", Lazy(() => m.Value.InitialRates));
        check.Output(c, "displacements", Lazy(() => m.Value.Displacements));
        check.Output(c, "pseudoRoots", Lazy(() => Steps(m.Value, j => Rows(m.Value.PseudoRoot(j)))));
        check.Output(c, "covariances", Lazy(() => Steps(m.Value, j => Rows(m.Value.Covariance(j)))));
        check.Output(c, "totalCovariances", Lazy(() => Steps(m.Value, j => Rows(m.Value.TotalCovariance(j)))));
        check.Output(
            c,
            "timeDependentVolatilities",
            Lazy(() => Enumerable.Range(0, m.Value.NumberOfRates).Select(i => m.Value.TimeDependentVolatility(i)).ToArray()));
    }

    /// <summary>
    /// Checks a curve state's reads from its first valid index, as the harness's stateReads writes them, with
    /// <c>swapRates</c> where the case holds it.
    /// </summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="cs">The curve state, set from the index.</param>
    /// <param name="first">The first valid index.</param>
    /// <param name="spans">The spannings read.</param>
    public static void CheckReads(VectorAssert check, VectorCase c, CurveState cs, int first, int[] spans)
    {
        int n = cs.NumberOfRates;
        check.Output(c, "discountRatios", Enumerable.Range(first, n - first + 1).Select(i => cs.DiscountRatio(i, n)).ToArray());
        check.Output(c, "forwardRates", Enumerable.Range(first, n - first).Select(cs.ForwardRate).ToArray());
        check.Output(c, "coterminalSwapRates", Enumerable.Range(first, n - first).Select(cs.CoterminalSwapRate).ToArray());
        check.Output(
            c, "coterminalSwapAnnuities", Enumerable.Range(first, n - first).Select(i => cs.CoterminalSwapAnnuity(n, i)).ToArray());
        check.Output(
            c,
            "cmSwapRates",
            spans.Select(s => Enumerable.Range(first, n - first).Select(i => cs.CmSwapRate(i, s)).ToArray()).ToArray());
        check.Output(
            c,
            "cmSwapAnnuities",
            spans.Select(s => Enumerable.Range(first, n - first).Select(i => cs.CmSwapAnnuity(n, i, s)).ToArray()).ToArray());
        check.Output(c, "forwardRatesVector", cs.ForwardRates()[first..].ToArray());
        check.Output(c, "coterminalSwapRatesVector", cs.CoterminalSwapRates()[first..].ToArray());
        check.Output(c, "cmSwapRatesVectors", spans.Select(s => cs.CmSwapRates(s)[first..].ToArray()).ToArray());
        if (c.Expected.TryGetProperty("swapRates", out _))
        {
            check.Output(
                c,
                "swapRates",
                Enumerable.Range(first, n - first)
                    .Select(b => Enumerable.Range(b + 1, n - b).Select(e => cs.SwapRate(b, e)).ToArray())
                    .ToArray());
        }
    }

    /// <summary>Applies an action to a value and returns the value, for lazy outputs that set up what they return.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="x">The value.</param>
    /// <param name="a">The action.</param>
    /// <returns>The value.</returns>
    public static T Also<T>(this T x, Action<T> a)
    {
        a(x);
        return x;
    }

    /// <summary>A lazily evaluated output, so a refusal is the output's throw.</summary>
    /// <param name="f">The output.</param>
    /// <returns>The function.</returns>
    public static Func<object?> Lazy(Func<object?> f) => f;

    private static double[][][] Steps(MarketModel m, Func<int, double[][]> read) =>
        [.. Enumerable.Range(0, m.NumberOfSteps).Select(read)];
}
