// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.AsianInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

[Trait("Oracle", "QuantLib")]
public class MCDiscreteArithmeticASEngineVectorTests
{
    // the generator's samples
    private const int Samples = 511;

    [Fact]
    public void MCDiscreteArithmeticASEngineVector()
    {
        var file = VectorFile.Load("pricingengines/asian/mc_discr_arith_av_strike");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement e = c.Input("engine");
            CheckResults(check, c, () =>
            {
                var option = DiscreteOptionOf(c.Input("option"), context);
                return MCDiscreteArithmeticASEngine.Calculate<PseudoRandom, GeneralStatistics>(
                    option, context, process, e.GetProperty("brownianBridge").GetBoolean(), e.GetProperty("antitheticVariate").GetBoolean(),
                    Samples, null, null, e.GetProperty("seed").GetInt64());
            });
        }

        check.Complete();
    }
}
