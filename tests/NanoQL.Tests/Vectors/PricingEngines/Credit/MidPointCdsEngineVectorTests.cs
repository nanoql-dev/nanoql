// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The mid-point CDS engine vectors, <c>tests/vectors/pricingengines/credit/midpointcdsengine.json</c>:
/// <see cref="MidPointCdsEngine"/>'s results on ten swaps, four credit curves, two recoveries and two evaluation dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MidPointCdsEngineVectorTests
{
    [Fact]
    public void MidPointCdsEngineVector()
    {
        var file = VectorFile.Load("pricingengines/credit/midpointcdsengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CreditInputs.CheckResults(
                check,
                c,
                MidPointCdsEngine.Calculate(
                    CreditInputs.SwapOf(c.Input("swap"), context),
                    context,
                    CreditInputs.CurveOf(c.Input("credit"), context),
                    c.Double("recoveryRate"),
                    CreditInputs.DiscountOf(c.Input("discount"))));
        }

        check.Complete();
    }
}
