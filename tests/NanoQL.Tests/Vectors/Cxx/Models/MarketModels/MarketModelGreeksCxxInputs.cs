// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model Greek vectors' inputs and checks
/// (<c>tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp</c>): the pathwise discounter, the pathwise
/// and proxy engines, the pseudo-root Jacobians, the pseudo-derivatives and the vega bumps.
/// </summary>
internal static class MarketModelGreeksCxxInputs
{
    /// <summary>Checks the pathwise discounter's factors.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckDiscounter(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] rateTimes = c.Doubles("rateTimes");
            Matrix discounts = MatrixOf(c.Input("discounts"));
            double[] factors = new double[rateTimes.Length];
            new MarketModelPathwiseDiscounter(c.Double("paymentTime"), rateTimes)
                .GetFactors(new Matrix(discounts.Rows, rateTimes.Length - 1, 0.0), discounts, c.Int("step"), factors);
            check.Output(c, "factors", factors);
        }

        check.Complete();
    }

    /// <summary>Checks the pathwise engines' prices and their error estimates.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckEngines(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            var evolver = new LogNormalFwdRateEuler(model, FactoryOf(c.Input("generator")), c.Ints("numeraires"));
            MarketModelPathwiseMultiProduct product = MarketModelProductCxxInputs.PathwiseProductOf(c.Input("product"));
            double initial = c.Double("initialNumeraireValue");
            int paths = c.Int("paths");
            int n = product.NumberOfProducts;
            double[] prices = new double[n], errors = new double[n];
            if (c.String("engine") == "PathwiseAccountingEngine")
            {
                var stats = new SequenceStatisticsInc(n * (model.NumberOfRates + 1));
                new PathwiseAccountingEngine(evolver, product, model, initial).MultiplePathValues(stats, paths);
                ImmutableArray<double> means = stats.Mean(), e = stats.ErrorEstimate();
                means.AsSpan(0, n).CopyTo(prices);
                e.AsSpan(0, n).CopyTo(errors);
            }
            else
            {
                Matrix[][] bumps = [.. c.Input("bumps").EnumerateArray().Select(s => s.EnumerateArray().Select(MatrixOf).ToArray())];
                double[] means, e;
                if (c.String("engine") == "PathwiseVegasAccountingEngine")
                {
                    var engine = new PathwiseVegasAccountingEngine(evolver, product, model, bumps, initial);
                    means = new double[engine.NumberOfResults];
                    e = new double[engine.NumberOfResults];
                    engine.MultiplePathValues(means, e, paths);
                }
                else
                {
                    var engine = new PathwiseVegasOuterAccountingEngine(evolver, product, model, bumps, initial);
                    means = new double[engine.NumberOfResults];
                    e = new double[engine.NumberOfResults];
                    engine.MultiplePathValues(means, e, paths);
                }

                int entries = 1 + model.NumberOfRates + bumps[0].Length;
                for (int i = 0; i < n; ++i)
                {
                    prices[i] = means[i * entries];
                    errors[i] = e[i * entries];
                }
            }

            check.Output(c, "prices", prices);
            check.Output(c, "priceErrors", errors);
        }

        check.Complete();
    }

    /// <summary>
    /// Checks the proxy Greek engine's values and Greeks, each constrained evolver's reference its own model,
    /// QuantLib's weight.
    /// </summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckProxy(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            BrownianGeneratorFactory factory = FactoryOf(c.Input("generator"));
            int[] numeraires = c.Ints("numeraires");
            int[] start = c.Ints("startIndexOfConstraint"), end = c.Ints("endIndexOfConstraint");
            ConstrainedEvolver[][] groups =
            [
                .. c.Input("groups").EnumerateArray().Select(g => g.EnumerateArray().Select(spec =>
                {
                    MarketModel bumped = ModelOf(spec);
                    var e = new LogNormalFwdRateEulerConstrained(bumped, bumped, factory, numeraires);
                    e.SetConstraintType(start, end);
                    return (ConstrainedEvolver)e;
                }).ToArray()),
            ];
            double[][][] weights =
                [.. c.Input("diffWeights").EnumerateArray().Select(g => g.EnumerateArray().Select(Reals).ToArray())];
            MarketModelMultiProduct product = MarketModelProductCxxInputs.ProductOf(c.Input("product"));
            int n = product.NumberOfProducts;
            var engine = new ProxyGreekEngine(
                new LogNormalFwdRateEuler(model, factory, numeraires), groups, weights, start, end, product, c.Double("initialNumeraireValue"));
            var stats = new SequenceStatisticsInc(n);
            SequenceStatisticsInc[][] greeks = [.. weights.Select(g => g.Select(_ => new SequenceStatisticsInc(n)).ToArray())];
            engine.MultiplePathValues(stats, greeks, c.Int("paths"));
            check.Output(c, "mean", stats.Mean());
            check.Output(c, "errorEstimate", stats.ErrorEstimate());
            check.Output(c, "greeks", greeks.Select(g => g.Select(s => s.Mean().ToArray()).ToArray()).ToArray());
        }

        check.Complete();
    }

    /// <summary>Checks the pseudo-root Jacobians' bumps.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckJacobians(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix root = MatrixOf(c.Input("pseudoRoot"));
            int alive = c.Int("aliveIndex"), numeraire = c.Int("numeraire");
            double[] taus = c.Doubles("taus"), displacements = c.Doubles("displacements");
            Matrix[] bumps = [.. c.Input("pseudoBumps").EnumerateArray().Select(MatrixOf)];
            double[] oldRates = c.Doubles("oldRates"), dfs = c.Doubles("oneStepDFs"), newRates = c.Doubles("newRates");
            double[] gaussians = c.Doubles("gaussians");
            int n = taus.Length;
            switch (c.String("kind"))
            {
                case "AllElements":
                {
                    Matrix[] b = [.. Enumerable.Range(0, n).Select(_ => new Matrix(n, root.Columns, 0.0))];
                    new RatePseudoRootJacobianAllElements(root, alive, numeraire, taus, displacements).GetBumps(oldRates, dfs, newRates, gaussians, b);
                    check.Output(c, "bumps", b.Select(Rows).ToArray());
                    break;
                }

                case "Numerical":
                {
                    var b = new Matrix(bumps.Length, n, 0.0);
                    new RatePseudoRootJacobianNumerical(root, alive, numeraire, taus, bumps, displacements).GetBumps(oldRates, dfs, newRates, gaussians, b);
                    check.Output(c, "bumps", Rows(b));
                    break;
                }

                default:
                {
                    var b = new Matrix(bumps.Length, n, 0.0);
                    new RatePseudoRootJacobian(root, alive, numeraire, taus, bumps, displacements).GetBumps(oldRates, dfs, newRates, gaussians, b);
                    check.Output(c, "bumps", Rows(b));
                    break;
                }
            }
        }

        check.Complete();
    }

    /// <summary>Checks the swaption and cap pseudo-derivatives.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckPseudoDerivatives(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            int steps = model.NumberOfSteps;
            if (c.String("kind") == "Swaption")
            {
                var d = new SwaptionPseudoDerivative(model, c.Int("startIndex"), c.Int("endIndex"));
                check.Output(c, "impliedVolatility", d.ImpliedVolatility);
                check.Output(c, "variance", d.Variance);
                check.Output(c, "expiry", d.Expiry);
                check.Output(c, "varianceDerivatives", Enumerable.Range(0, steps).Select(i => Rows(d.VarianceDerivative(i))).ToArray());
                check.Output(c, "volatilityDerivatives", Enumerable.Range(0, steps).Select(i => Rows(d.VolatilityDerivative(i))).ToArray());
            }
            else
            {
                var d = new CapPseudoDerivative(model, c.Double("strike"), c.Int("startIndex"), c.Int("endIndex"), c.Double("firstDF"));
                check.Output(c, "impliedVolatility", d.ImpliedVolatility);
                check.Output(c, "priceDerivatives", Enumerable.Range(0, steps).Select(i => Rows(d.PriceDerivative(i))).ToArray());
                check.Output(c, "volatilityDerivatives", Enumerable.Range(0, steps).Select(i => Rows(d.VolatilityDerivative(i))).ToArray());
            }
        }

        check.Complete();
    }

    /// <summary>Checks the vega bump clusters' checks and the default collections.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckBumpClusters(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            if (c.String("kind") == "Cluster")
            {
                VegaBumpCluster cluster = ClusterOf(c.Input("cluster")), comparee = ClusterOf(c.Input("comparee"));
                check.Output(c, "doesIntersect", cluster.DoesIntersect(comparee));
                check.Output(c, "isCompatible", cluster.IsCompatible(model));
            }
            else
            {
                var collection = new VegaBumpCollection(model, c.Bool("factorwise"));
                check.Output(c, "numberBumps", collection.NumberBumps);
                check.Output(
                    c,
                    "allBumps",
                    collection.AllBumps.Select(b => new[] { b.FactorBegin, b.FactorEnd, b.RateBegin, b.RateEnd, b.StepBegin, b.StepEnd }).ToArray());
                check.Output(c, "isFull", collection.IsFull());
                check.Output(c, "isNonOverlapping", collection.IsNonOverlapping());
                check.Output(c, "isSensible", collection.IsSensible());
            }
        }

        check.Complete();
    }

    /// <summary>Checks the bump instrument Jacobians and the orthogonalized bump finders.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckBumpInstruments(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var bumps = new VegaBumpCollection(ModelOf(c.Input("model")), c.Bool("factorwise"));
            VolatilityBumpInstrumentJacobianSwaption[] swaptions =
                [.. c.Input("swaptions").EnumerateArray().Select(s => new VolatilityBumpInstrumentJacobianSwaption(s[0].GetInt32(), s[1].GetInt32()))];
            VolatilityBumpInstrumentJacobianCap[] caps =
                [.. c.Input("caps").EnumerateArray().Select(s => new VolatilityBumpInstrumentJacobianCap(s[0].GetInt32(), s[1].GetInt32(), s[2].GetDouble()))];
            if (c.String("kind") == "Jacobian")
            {
                var j = new VolatilityBumpInstrumentJacobian(bumps, swaptions, caps);
                int count = swaptions.Length + caps.Length;
                check.Output(c, "derivativesVolatility", Enumerable.Range(0, count).Select(i => j.DerivativesVolatility(i).ToArray()).ToArray());
                check.Output(c, "onePercentBumps", Enumerable.Range(0, count).Select(i => j.OnePercentBump(i).ToArray()).ToArray());
                check.Output(c, "allOnePercentBumps", Rows(j.GetAllOnePercentBumps()));
            }
            else
            {
                var finder = new OrthogonalizedBumpFinder(bumps, swaptions, caps, c.Double("multiplierCutOff"), c.Double("tolerance"));
                check.Output(c, "bumps", finder.GetVegaBumps().Select(s => s.Select(Rows).ToArray()).ToArray());
            }
        }

        check.Complete();
    }

    private static VegaBumpCluster ClusterOf(JsonElement e)
    {
        int[] c = Integers(e);
        return new VegaBumpCluster(c[0], c[1], c[2], c[3], c[4], c[5]);
    }
}
