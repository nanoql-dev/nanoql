// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model product specs, as tools/cxx/src/models/marketmodels/products/productspec.hpp reads
/// them, and the product vectors' checks: a product stepped on LMM curve states set on each step's forwards, read per
/// step as products.cpp reads QuantLib's, and its values through the accounting engine as accountingengine.cpp reads
/// QuantLib's.
/// </summary>
internal static class MarketModelProductCxxInputs
{
    /// <summary>The product a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new product.</returns>
    public static MarketModelMultiProduct ProductOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        double[] V(string name) => Reals(spec.GetProperty(name));
        double R(string name) => spec.GetProperty(name).GetDouble();
        int I(string name) => spec.GetProperty(name).GetInt32();
        bool B(string name) => spec.GetProperty(name).GetBoolean();
        switch (type)
        {
            case "MultiStepForwards":
                return new MultiStepForwards(V("rateTimes"), V("accruals"), V("paymentTimes"), V("strikes"));
            case "MultiStepOptionlets":
                return new MultiStepOptionlets(V("rateTimes"), V("accruals"), V("paymentTimes"), Payoffs(spec.GetProperty("payoffs")));
            case "MultiStepNothing":
                return new MultiStepNothing(
                    new EvolutionDescription(V("rateTimes"), V("evolutionTimes")), I("numberOfProducts"), I("doneIndex"));
            case "MarketModelCashRebate":
                return new MarketModelCashRebate(
                    new EvolutionDescription(V("rateTimes"), V("evolutionTimes")),
                    V("paymentTimes"),
                    MatrixOf(spec.GetProperty("amounts")),
                    I("numberOfProducts"));
            case "MultiStepSwap":
                return new MultiStepSwap(
                    V("rateTimes"), V("fixedAccruals"), V("floatingAccruals"), V("paymentTimes"), R("fixedRate"), B("payer"));
            case "MultiStepSwaption":
                return new MultiStepSwaption(V("rateTimes"), I("startIndex"), I("endIndex"), PayoffOf(spec.GetProperty("payoff")));
            case "MultiStepCoterminalSwaps":
                return new MultiStepCoterminalSwaps(
                    V("rateTimes"), V("fixedAccruals"), V("floatingAccruals"), V("paymentTimes"), R("fixedRate"));
            case "MultiStepCoterminalSwaptions":
                return new MultiStepCoterminalSwaptions(V("rateTimes"), V("paymentTimes"), Striked(spec.GetProperty("payoffs")));
            case "MultiStepCoinitialSwaps":
                return new MultiStepCoinitialSwaps(
                    V("rateTimes"), V("fixedAccruals"), V("floatingAccruals"), V("paymentTimes"), R("fixedRate"));
            case "MultiStepInverseFloater":
                return new MultiStepInverseFloater(
                    V("rateTimes"),
                    V("fixedAccruals"),
                    V("floatingAccruals"),
                    V("fixedStrikes"),
                    V("fixedMultipliers"),
                    V("floatingSpreads"),
                    V("paymentTimes"),
                    B("payer"));
            case "MultiStepRatchet":
                return new MultiStepRatchet(
                    V("rateTimes"),
                    V("accruals"),
                    V("paymentTimes"),
                    R("gearingOfFloor"),
                    R("gearingOfFixing"),
                    R("spreadOfFloor"),
                    R("spreadOfFixing"),
                    R("initialFloor"),
                    B("payer"));
            case "MultiStepTarn":
                return new MultiStepTarn(
                    V("rateTimes"),
                    V("accruals"),
                    V("accrualsFloating"),
                    V("paymentTimes"),
                    V("paymentTimesFloating"),
                    R("totalCoupon"),
                    V("strikes"),
                    V("multipliers"),
                    V("floatingSpreads"));
            case "MultiStepPeriodCapletSwaptions":
                return new MultiStepPeriodCapletSwaptions(
                    V("rateTimes"),
                    V("forwardOptionPaymentTimes"),
                    V("swaptionPaymentTimes"),
                    Striked(spec.GetProperty("forwardPayOffs")),
                    Striked(spec.GetProperty("swapPayOffs")),
                    I("period"),
                    I("offset"));
            case "OneStepForwards":
                return new OneStepForwards(V("rateTimes"), V("accruals"), V("paymentTimes"), V("strikes"));
            case "OneStepOptionlets":
                return new OneStepOptionlets(V("rateTimes"), V("accruals"), V("paymentTimes"), Payoffs(spec.GetProperty("payoffs")));
            case "OneStepCoinitialSwaps":
                return new OneStepCoinitialSwaps(
                    V("rateTimes"), V("fixedAccruals"), V("floatingAccruals"), V("paymentTimes"), R("fixedRate"));
            case "OneStepCoterminalSwaps":
                return new OneStepCoterminalSwaps(
                    V("rateTimes"), V("fixedAccruals"), V("floatingAccruals"), V("paymentTimes"), R("fixedRate"));
            case "MultiProductPathwiseWrapper":
                return new MultiProductPathwiseWrapper(PathwiseProductOf(spec.GetProperty("inner")));
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

    /// <summary>The pathwise product a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>A new product.</returns>
    public static MarketModelPathwiseMultiProduct PathwiseProductOf(JsonElement spec)
    {
        double[] V(string name) => Reals(spec.GetProperty(name));
        double R(string name) => spec.GetProperty(name).GetDouble();
        return spec.GetProperty("type").GetString() switch
        {
            "MarketModelPathwiseMultiCaplet" =>
                new MarketModelPathwiseMultiCaplet(V("rateTimes"), V("accruals"), V("paymentTimes"), V("strikes")),
            "MarketModelPathwiseMultiDeflatedCaplet" => spec.TryGetProperty("strikes", out _)
                ? new MarketModelPathwiseMultiDeflatedCaplet(V("rateTimes"), V("accruals"), V("paymentTimes"), V("strikes"))
                : new MarketModelPathwiseMultiDeflatedCaplet(V("rateTimes"), V("accruals"), V("paymentTimes"), R("strike")),
            "MarketModelPathwiseMultiDeflatedCap" => new MarketModelPathwiseMultiDeflatedCap(
                V("rateTimes"),
                V("accruals"),
                V("paymentTimes"),
                R("strike"),
                [.. spec.GetProperty("startsAndEnds").EnumerateArray().Select(x => (x[0].GetInt32(), x[1].GetInt32()))]),
            "MarketModelPathwiseCashRebate" => new MarketModelPathwiseCashRebate(
                new EvolutionDescription(V("rateTimes"), V("evolutionTimes")),
                V("paymentTimes"),
                MatrixOf(spec.GetProperty("amounts")),
                spec.GetProperty("numberOfProducts").GetInt32()),
            "MarketModelPathwiseInverseFloater" => new MarketModelPathwiseInverseFloater(
                V("rateTimes"),
                V("fixedAccruals"),
                V("floatingAccruals"),
                V("fixedStrikes"),
                V("fixedMultipliers"),
                V("floatingSpreads"),
                V("paymentTimes"),
                spec.GetProperty("payer").GetBoolean()),
            "MarketModelPathwiseSwap" => new MarketModelPathwiseSwap(V("rateTimes"), V("accruals"), V("strikes"), R("multiplier")),
            "MarketModelPathwiseCoterminalSwaptionsDeflated" =>
                new MarketModelPathwiseCoterminalSwaptionsDeflated(V("rateTimes"), V("strikes")),
            _ => new MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(V("rateTimes"), V("strikes"), R("bumpSize")),
        };
    }

    /// <summary>
    /// Checks a product vector file's cases: each product stepped on its case's states, or valued through the
    /// accounting engine where the case names an evolver.
    /// </summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckSteps(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("evolver", out _))
            {
                CheckEngine(check, c);
                continue;
            }

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

    /// <summary>
    /// Checks a pathwise product vector file's cases: each product stepped on its case's states, its cash flows'
    /// amounts sized 1 + the number of rates as QuantLib's engines size them.
    /// </summary>
    /// <param name="path">The vector file's path under tests/vectors/.</param>
    public static void CheckPathwiseSteps(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModelPathwiseMultiProduct p = PathwiseProductOf(c.Input("product"));
            double[][] forwards = [.. c.Input("stepForwards").EnumerateArray().Select(Reals)];
            int n = p.NumberOfProducts;
            int size = p.Evolution.RateTimes.Length;
            check.Output(c, "numberOfProducts", n);
            check.Output(c, "maxNumberOfCashFlowsPerProductPerStep", p.MaxNumberOfCashFlowsPerProductPerStep);
            check.Output(c, "alreadyDeflated", p.AlreadyDeflated);
            check.Output(c, "possibleCashFlowTimes", p.PossibleCashFlowTimes());
            check.Output(c, "evolutionTimes", p.Evolution.EvolutionTimes);
            check.Output(c, "suggestedNumeraires", Lazy(() => p.SuggestedNumeraires()));

            int[] counts = new int[n];
            MarketModelPathwiseMultiProductCashFlow[][] flows =
            [
                .. Enumerable.Range(0, n).Select(_ => Enumerable.Range(0, p.MaxNumberOfCashFlowsPerProductPerStep)
                    .Select(_ => new MarketModelPathwiseMultiProductCashFlow(size)).ToArray()),
            ];
            var done = new List<bool>();
            var numbers = new List<int[]>();
            var indices = new List<int[][]>();
            var amounts = new List<double[][][]>();
            p.Reset();
            foreach (double[] f in forwards)
            {
                var cs = new LMMCurveState(p.Evolution.RateTimes.AsSpan());
                cs.SetOnForwardRates(f);
                bool d = p.NextTimeStep(cs, counts, flows);
                done.Add(d);
                numbers.Add((int[])counts.Clone());
                indices.Add([.. Enumerable.Range(0, n).Select(i => flows[i][..counts[i]].Select(x => x.TimeIndex).ToArray())]);
                amounts.Add([.. Enumerable.Range(0, n).Select(i => flows[i][..counts[i]].Select(x => x.Amount.ToArray()).ToArray())]);
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

    /// <summary>
    /// Checks an accounting-engine case: the evolver of the case's type on its model and generator, the product
    /// valued over the paths, read through the statistics' samples, weight sum, means, variances and error estimates.
    /// </summary>
    /// <param name="check">The file's check.</param>
    /// <param name="c">The case.</param>
    public static void CheckEngine(VectorAssert check, VectorCase c)
    {
        MarketModel model = ModelOf(c.Input("model"));
        BrownianGeneratorFactory factory = FactoryOf(c.Input("generator"));
        int[] numeraires = Integers(c.Input("numeraires"));
        MarketModelEvolver evolver = c.String("evolver") switch
        {
            "LogNormalFwdRateEuler" => new LogNormalFwdRateEuler(model, factory, numeraires),
            "LogNormalFwdRatePc" => new LogNormalFwdRatePc(model, factory, numeraires),
            _ => new NormalFwdRatePc(model, factory, numeraires),
        };
        MarketModelMultiProduct product = ProductOf(c.Input("product"));
        var engine = new AccountingEngine(evolver, product, c.Double("initialNumeraireValue"));
        var stats = new SequenceStatisticsInc(product.NumberOfProducts);
        engine.MultiplePathValues(stats, c.Input("paths").GetInt32());
        check.Output(c, "samples", stats.Samples);
        check.Output(c, "weightSum", stats.WeightSum());
        check.Output(c, "mean", stats.Mean());
        check.Output(c, "variance", Lazy(() => stats.Variance()));
        check.Output(c, "errorEstimate", Lazy(() => stats.ErrorEstimate()));
    }

    private static Payoff[] Payoffs(JsonElement payoffs) =>
        [.. payoffs.EnumerateArray().Select(p => (Payoff)new PlainVanillaPayoff((OptionType)p[0].GetInt32(), p[1].GetDouble()))];

    private static StrikedTypePayoff PayoffOf(JsonElement p) =>
        new PlainVanillaPayoff((OptionType)p[0].GetInt32(), p[1].GetDouble());

    private static StrikedTypePayoff[] Striked(JsonElement payoffs) => [.. payoffs.EnumerateArray().Select(PayoffOf)];
}
