// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.AsianInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class ContinuousArithmeticAsianLevyEngineVectorTests
{
    [Fact]
    public void ContinuousArithmeticAsianLevyEngineVector()
    {
        var file = VectorFile.Load("pricingengines/asian/continuousarithmeticasianlevyengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            CheckResults(check, c, () => ContinuousArithmeticAsianLevyEngine.Calculate(ContinuousOptionOf(c.Input("option"), context), context, process, c.Double("currentAverage")));
        }

        check.Complete();
    }
}
