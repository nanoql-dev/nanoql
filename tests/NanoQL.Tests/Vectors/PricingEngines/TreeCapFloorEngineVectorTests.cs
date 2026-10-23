// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="TreeCapFloorEngine"/>'s vectors, <c>tests/vectors/pricingengines/capfloor/treecapfloorengine.json</c>:
/// forward-starting caps, floors and collars and an instrument paid in full, on Hull-White's fitted tree with time
/// steps, on a time grid and on a lattice built once and priced on twice (QuantLib's grid engine reusing its own, the
/// oracle of nanoql's lattice overload), on Black-Karasinski's, on Vasicek's with the curve given, and on G2's
/// two-dimensional tree.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TreeCapFloorEngineVectorTests
{
    [Fact]
    public void TreeCapFloorEngineVector()
    {
        var file = VectorFile.Load("pricingengines/capfloor/treecapfloorengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            double[] gridTimes = c.Doubles("gridTimes");
            int gridSteps = c.Int("gridSteps");
            check.Output(c, "NPV", c.Input("engines").EnumerateArray().Select(e => (Func<object?>)(() =>
            {
                CapFloor capFloor = CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
                (ShortRateModel model, YieldTermStructure? termStructure) = ModelOf(e, curve);
                if (e.TryGetProperty("reused", out _))
                {
                    // The lattice built once, priced on twice, as QuantLib's grid engine reuses its own.
                    Lattice lattice = model.Tree(new TimeGrid(gridTimes, gridSteps));
                    TreeCapFloorEngine.Calculate(capFloor, context, model, lattice, termStructure);
                    return TreeCapFloorEngine.Calculate(CapFloorInputs.CapFloorOf(c.Input("capFloor"), context), context, model, lattice, termStructure).NPV;
                }

                return e.TryGetProperty("grid", out _)
                    ? TreeCapFloorEngine.Calculate(capFloor, context, model, new TimeGrid(gridTimes, gridSteps), termStructure).NPV
                    : TreeCapFloorEngine.Calculate(capFloor, context, model, e.GetProperty("steps").GetInt32(), termStructure).NPV;
            })));
        }

        check.Complete();
    }

    // The engine's model, and the curve given to the engine for a model with none of its own.
    private static (ShortRateModel Model, YieldTermStructure? TermStructure) ModelOf(JsonElement e, YieldTermStructure curve)
    {
        double Real(string name) => IndexInputs.Real(e.GetProperty(name));
        return e.GetProperty("model").GetString() switch
        {
            "HullWhite" => (new HullWhite(curve, Real("a"), Real("sigma")), null),
            "BlackKarasinski" => (new BlackKarasinski(curve, Real("a"), Real("sigma")), null),
            "Vasicek" => (new Vasicek(Real("r0"), Real("a"), Real("b"), Real("sigma"), Real("lambda")), curve),
            "G2" => (new G2(curve, Real("a"), Real("sigma"), Real("b"), Real("eta"), Real("rho")), null),
            string model => throw new InvalidOperationException($"unknown model '{model}'"),
            null => throw new InvalidOperationException("an engine without a model"),
        };
    }
}
