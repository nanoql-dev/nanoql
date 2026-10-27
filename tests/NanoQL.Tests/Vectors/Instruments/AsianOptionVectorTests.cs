// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.AsianInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class AsianOptionVectorTests
{
    [Fact]
    public void AsianOptionVector()
    {
        var file = VectorFile.Load("instruments/asianoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            check.Output(
                c,
                "NPV",
                Lazy(() =>
                {
                    DiscreteAveragingAsianOption option = DiscreteOptionOf(c.Input("option"), context);
                    return option.AverageType == AverageType.Arithmetic
                        ? TurnbullWakemanAsianEngine.Calculate(option, context, process).NPV
                        : AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option, context, process).NPV;
                }));
        }

        check.Complete();
    }
}
