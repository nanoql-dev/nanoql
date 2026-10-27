// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.AsianInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class AnalyticDiscreteGeometricAverageStrikeAsianEngineVectorTests
{
    [Fact]
    public void AnalyticDiscreteGeometricAverageStrikeAsianEngineVector()
    {
        var file = VectorFile.Load("pricingengines/asian/analytic_discr_geom_av_strike");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            CheckResults(check, c, () => AnalyticDiscreteGeometricAverageStrikeAsianEngine.Calculate(DiscreteOptionOf(c.Input("option"), context), context, process));
        }

        check.Complete();
    }
}
