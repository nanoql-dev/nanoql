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
/// <see cref="AnalyticCapFloorEngine"/>'s vectors, <c>tests/vectors/pricingengines/capfloor/analyticcapfloorengine.json</c>:
/// caps, floors and collars, seasoned, fixing today and expired among them, priced on Hull-White on its own curve, on
/// Vasicek and Cox-Ingersoll-Ross with the curve given, and on extended Cox-Ingersoll-Ross on its own; and the
/// instruments the engine or the cap refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticCapFloorEngineVectorTests
{
    [Fact]
    public void AnalyticCapFloorEngineVector()
    {
        var file = VectorFile.Load("pricingengines/capfloor/analyticcapfloorengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            check.Output(c, "NPV", c.Input("engines").EnumerateArray().Select(e => (Func<object?>)(() =>
            {
                CapFloor capFloor = CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
                (IAffineModel model, YieldTermStructure? termStructure) = ModelOf(e, curve);
                return AnalyticCapFloorEngine.Calculate(capFloor, context, model, termStructure).NPV;
            })));
        }

        check.Complete();
    }

    // The engine's model, and the curve given to the engine for a model with none of its own.
    internal static (IAffineModel Model, YieldTermStructure? TermStructure) ModelOf(JsonElement e, YieldTermStructure curve)
    {
        double Real(string name) => IndexInputs.Real(e.GetProperty(name));
        return e.GetProperty("model").GetString() switch
        {
            "HullWhite" => (new HullWhite(curve, Real("a"), Real("sigma")), null),
            "Vasicek" => (new Vasicek(Real("r0"), Real("a"), Real("b"), Real("sigma"), Real("lambda")), curve),
            "CoxIngersollRoss" => (new CoxIngersollRoss(Real("r0"), Real("theta"), Real("k"), Real("sigma")), curve),
            "ExtendedCoxIngersollRoss" => (new ExtendedCoxIngersollRoss(curve, Real("theta"), Real("k"), Real("sigma"), Real("x0")), null),
            string model => throw new InvalidOperationException($"unknown model '{model}'"),
            null => throw new InvalidOperationException("an engine without a model"),
        };
    }
}
