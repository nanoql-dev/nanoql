// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.AsianInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class AnalyticDiscreteGeometricAveragePriceAsianHestonEngineVectorTests
{
    [Fact]
    public void AnalyticDiscreteGeometricAveragePriceAsianHestonEngineVector()
    {
        var file = VectorFile.Load("experimental/asian/analytic_discr_geom_av_price_heston");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            HestonProcess process = HestonProcessVectorTests.HestonProcessOf(c.Input("process"));
            CheckResults(
                check,
                c,
                () => AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(DiscreteOptionOf(c.Input("option"), context), context, process));
        }

        check.Complete();
    }
}
