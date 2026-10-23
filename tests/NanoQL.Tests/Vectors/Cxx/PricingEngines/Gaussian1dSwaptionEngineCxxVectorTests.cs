// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Gaussian1d swaption engine vectors,
/// <c>tests/vectors/cxx/pricingengines/swaption/gaussian1dswaptionengine.json</c>: the exercise probabilities of
/// <see cref="Gaussian1dSwaptionEngine"/>, an additional result the wheel doesn't read, Naive and Digital, on three GSR
/// models (<see cref="Gaussian1dInputs"/>), with and without a discount curve, with the NPV.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dSwaptionEngineCxxVectorTests
{
    [Fact]
    public void Gaussian1dSwaptionEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/swaption/gaussian1dswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            JsonElement curve = c.Input("discountCurve");
            YieldTermStructure? discountCurve = curve.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(curve);
            InstrumentResults results = Gaussian1dSwaptionEngine.Calculate(
                swaption, context, Gaussian1dInputs.GsrOf(c.Input("model")), 64, 7.0, true, false, discountCurve,
                (Gaussian1dSwaptionEngineProbabilities)c.Int("probabilities"));
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "probabilities", results.Result<ImmutableArray<double>>("probabilities").Select(p => (object?)p).ToArray());
        }

        check.Complete();
    }
}
