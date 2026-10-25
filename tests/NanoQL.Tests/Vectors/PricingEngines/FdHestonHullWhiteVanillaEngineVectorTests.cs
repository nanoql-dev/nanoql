// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdHestonHullWhiteVanillaEngine vectors,
/// <c>tests/vectors/pricingengines/vanilla/fdhestonhullwhitevanillaengine.json</c>: European and American options on
/// Heston models with Hull-White processes, every scheme with and without the control variate, damping steps, QuantLib's
/// default grid and an expired option: the NPV, delta, gamma and theta.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdHestonHullWhiteVanillaEngineVectorTests
{
    [Fact]
    public void FdHestonHullWhiteVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdhestonhullwhitevanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var priced = new Lazy<OneAssetOptionResults>(() => Price(c.Input("pricing"), context));
            (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
            [
                ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
            ];
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
            {
                check.Output(c, name, () => get(priced.Value));
            }
        }

        check.Complete();
    }

    private static OneAssetOptionResults Price(JsonElement spec, EvaluationContext context)
    {
        HestonProcess process = HestonProcessVectorTests.HestonProcessOf(spec.GetProperty("process"));
        var model = new HestonModel(process);
        HullWhiteProcess hullWhite = FdmHestonHullWhiteOpVectorTests.HullWhiteProcessOf(spec.GetProperty("hullWhite"), process);
        var option = new VanillaOption(PayoffOf(spec.GetProperty("payoff")), FdExerciseOf(spec.GetProperty("exercise"), context));
        FdmSchemeDesc scheme = SchemeOf(spec.GetProperty("scheme"));
        return FdHestonHullWhiteVanillaEngine.Calculate(
            option,
            context,
            model,
            hullWhite,
            spec.GetProperty("correlation").GetDouble(),
            spec.GetProperty("tGrid").GetInt32(),
            spec.GetProperty("xGrid").GetInt32(),
            spec.GetProperty("vGrid").GetInt32(),
            spec.GetProperty("rGrid").GetInt32(),
            spec.GetProperty("dampingSteps").GetInt32(),
            spec.GetProperty("controlVariate").GetBoolean(),
            scheme);
    }
}
