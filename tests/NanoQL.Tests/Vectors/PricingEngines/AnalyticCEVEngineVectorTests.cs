// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticCEVEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticcevengine.json</c>: the NPVs of
/// European calls and puts on CEV forwards over elasticities on both sides of 1, an expired option and the refusal of
/// an American exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticCEVEngineVectorTests
{
    [Fact]
    public void AnalyticCEVEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticcevengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            double f0 = c.Double("f0"), alpha = c.Double("alpha"), beta = c.Double("beta");
            YieldTermStructure discount = CurveOf(c.Input("discountCurve"));
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            object?[] npvs =
            [
                .. c.Input("payoffs").EnumerateArray().Select(p => Lazy(() =>
                    AnalyticCEVEngine.Calculate(new VanillaOption(PayoffOf(p), exercise), context, f0, alpha, beta, discount)
                        .NPV)),
            ];
            check.Output(c, "NPV", npvs);
        }

        check.Complete();
    }
}
