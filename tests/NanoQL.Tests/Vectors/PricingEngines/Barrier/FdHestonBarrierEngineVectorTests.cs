// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdHestonBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/fdhestonbarrierengine.json</c>: the NPV,
/// delta, gamma and theta of the four barrier types with and without a rebate on three Heston parameter sets, and on the
/// first: QuantLib's default grid, damping steps, two schemes, cash dividends, a leverage function and a mixing factor on
/// knock-outs, and an expired option. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonBarrierEngineVectorTests
{
    [Fact]
    public void FdHestonBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/fdhestonbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("pricing");
            var priced = new Lazy<OneAssetOptionResults>(() =>
            {
                FdHestonPricing p = PricingOf(spec, context);
                JsonElement barrier = spec.GetProperty("barrier");
                var option = new BarrierOption(
                    (BarrierType)barrier.GetProperty("barrierType").GetInt32(),
                    barrier.GetProperty("barrier").GetDouble(),
                    barrier.GetProperty("rebate").GetDouble(),
                    p.Payoff,
                    p.Exercise);
                return FdHestonBarrierEngine.Calculate(
                    option, context, p.Model, p.Dividends, p.TGrid, p.XGrid, p.VGrid, p.DampingSteps, p.Scheme, p.Leverage,
                    p.MixingFactor);
            });
            CheckResults(check, c, priced);
        }

        check.Complete();
    }

    /// <summary>The engine arguments a pricing spec of
    /// <c>tools/vectors/gen/pricingengines/barrier/fdhestonbarrierengine.py</c> describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The arguments.</returns>
    internal static FdHestonPricing PricingOf(JsonElement spec, EvaluationContext context) => new(
        new HestonModel(HestonProcessVectorTests.HestonProcessOf(spec.GetProperty("process"))),
        PayoffOf(spec.GetProperty("payoff")),
        FdExerciseOf(spec.GetProperty("exercise"), context),
        DividendsOf(spec.GetProperty("dividends")),
        spec.GetProperty("tGrid").GetInt32(),
        spec.GetProperty("xGrid").GetInt32(),
        spec.GetProperty("vGrid").GetInt32(),
        spec.GetProperty("dampingSteps").GetInt32(),
        SchemeOf(spec.GetProperty("scheme")),
        FdHestonVanillaEngineVectorTests.LeverageOf(spec.GetProperty("leverage")),
        spec.GetProperty("mixingFactor").GetDouble());

    /// <summary>Compares a case's NPV, delta, gamma and theta.</summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="priced">The results, computed when compared.</param>
    internal static void CheckResults(VectorAssert check, VectorCase c, Lazy<OneAssetOptionResults> priced)
    {
        (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
        [
            ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
        ];
        foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
        {
            check.Output(c, name, () => get(priced.Value));
        }
    }

    /// <summary>An FD Heston barrier engine's arguments.</summary>
    internal sealed record FdHestonPricing(
        HestonModel Model,
        StrikedTypePayoff Payoff,
        Exercise Exercise,
        ImmutableArray<Dividend> Dividends,
        int TGrid,
        int XGrid,
        int VGrid,
        int DampingSteps,
        FdmSchemeDesc Scheme,
        LocalVolTermStructure? Leverage,
        double MixingFactor);
}
