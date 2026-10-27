// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model product specs, as tools/cxx/src/models/marketmodels/products/productspec.hpp reads
/// them, and the product vectors' check: a product stepped on LMM curve states set on each step's forwards, read per
/// step as products.cpp reads QuantLib's.
/// </summary>
internal static class MarketModelProductCxxInputs
{
    /// <summary>The product a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new product.</returns>
    public static MarketModelMultiProduct ProductOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        switch (type)
        {
            case "MultiStepForwards":
                return new MultiStepForwards(
                    Reals(spec.GetProperty("rateTimes")),
                    Reals(spec.GetProperty("accruals")),
                    Reals(spec.GetProperty("paymentTimes")),
                    Reals(spec.GetProperty("strikes")));
            case "MultiStepOptionlets":
                Payoff[] payoffs =
                [
                    .. spec.GetProperty("payoffs").EnumerateArray().Select(
                        p => (Payoff)new PlainVanillaPayoff((OptionType)p[0].GetInt32(), p[1].GetDouble())),
                ];
                return new MultiStepOptionlets(
                    Reals(spec.GetProperty("rateTimes")),
                    Reals(spec.GetProperty("accruals")),
                    Reals(spec.GetProperty("paymentTimes")),
                    payoffs);
            case "MultiStepNothing":
                return new MultiStepNothing(
                    new EvolutionDescription(Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("evolutionTimes"))),
                    spec.GetProperty("numberOfProducts").GetInt32(),
                    spec.GetProperty("doneIndex").GetInt32());
            case "MarketModelCashRebate":
                return new MarketModelCashRebate(
                    new EvolutionDescription(Reals(spec.GetProperty("rateTimes")), Reals(spec.GetProperty("evolutionTimes"))),
                    Reals(spec.GetProperty("paymentTimes")),
                    MatrixOf(spec.GetProperty("amounts")),
                    spec.GetProperty("numberOfProducts").GetInt32());
            default:
                MarketModelComposite c = type == "MultiProductComposite" ? new MultiProductComposite() : new SingleProductComposite();
                foreach (JsonElement part in spec.GetProperty("parts").EnumerateArray())
                {
                    c.Add(ProductOf(part.GetProperty("product")), part.GetProperty("multiplier").GetDouble());
                }

                c.FinalizeComposite();
                return c;
        }
    }

    /// <summary>Checks a product vector file's cases: each product stepped on its case's states.</summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckSteps(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModelMultiProduct p = ProductOf(c.Input("product"));
            double[][] forwards = [.. c.Input("stepForwards").EnumerateArray().Select(Reals)];
            int n = p.NumberOfProducts;
            check.Output(c, "numberOfProducts", n);
            if (c.Expected.TryGetProperty("maxNumberOfCashFlowsPerProductPerStep", out _))
            {
                check.Output(c, "maxNumberOfCashFlowsPerProductPerStep", p.MaxNumberOfCashFlowsPerProductPerStep);
            }

            check.Output(c, "possibleCashFlowTimes", p.PossibleCashFlowTimes());
            check.Output(c, "evolutionTimes", p.Evolution.EvolutionTimes);
            check.Output(c, "suggestedNumeraires", Lazy(() => p.SuggestedNumeraires()));

            int[] counts = new int[n];
            MarketModelMultiProductCashFlow[][] flows =
                [.. Enumerable.Range(0, n).Select(_ => new MarketModelMultiProductCashFlow[p.MaxNumberOfCashFlowsPerProductPerStep])];
            var done = new List<bool>();
            var numbers = new List<int[]>();
            var indices = new List<int[][]>();
            var amounts = new List<double[][]>();
            p.Reset();
            foreach (double[] f in forwards)
            {
                var cs = new LMMCurveState(p.Evolution.RateTimes.AsSpan());
                cs.SetOnForwardRates(f);
                bool d = p.NextTimeStep(cs, counts, flows);
                done.Add(d);
                numbers.Add((int[])counts.Clone());
                indices.Add([.. Enumerable.Range(0, n).Select(i => flows[i][..counts[i]].Select(x => x.TimeIndex).ToArray())]);
                amounts.Add([.. Enumerable.Range(0, n).Select(i => flows[i][..counts[i]].Select(x => x.Amount).ToArray())]);
                if (d)
                {
                    break;
                }
            }

            check.Output(c, "done", done);
            check.Output(c, "numberCashFlows", numbers);
            check.Output(c, "timeIndices", indices);
            check.Output(c, "amounts", amounts);
        }

        check.Complete();
    }
}
