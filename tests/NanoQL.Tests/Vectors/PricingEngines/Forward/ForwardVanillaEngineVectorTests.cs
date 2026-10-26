// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.ExoticInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ForwardEuropeanEngine vectors, <c>tests/vectors/pricingengines/forward/forwardengine.json</c>:
/// <see cref="ForwardVanillaEngine"/> over <see cref="AnalyticEuropeanEngine"/>, its NPV and Greeks for forward-start
/// calls and puts on flat markets and on zero curves under a flat volatility.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardVanillaEngineVectorTests
{
    [Fact]
    public void ForwardEuropeanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/forward/forwardengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ForwardVanillaOption option = ForwardOf(c.Input("option"), context);
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            var r = new Lazy<OneAssetOptionResults>(
                () => ForwardVanillaEngine.Calculate(option, context, process, AnalyticEuropeanEngine.Calculate));
            AnalyticCliquetEngineVectorTests.CheckGreeks(check, c, r);
        }

        check.Complete();
    }
}
