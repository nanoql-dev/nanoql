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
public class MCDiscreteArithmeticAPHestonEngineVectorTests
{
    // the generator's samples
    private const int Samples = 511;

    [Fact]
    public void MCDiscreteArithmeticAPHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/asian/mc_discr_arith_av_price_heston");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            HestonProcess process = HestonProcessVectorTests.HestonProcessOf(c.Input("process"));
            JsonElement e = c.Input("engine");
            CheckResults(check, c, () =>
            {
                var option = DiscreteOptionOf(c.Input("option"), context);
                bool antithetic = e.GetProperty("antitheticVariate").GetBoolean();
                long seed = e.GetProperty("seed").GetInt64();
                int? steps = NullableInt(e, "timeSteps"), perYear = NullableInt(e, "timeStepsPerYear");
                return LowDiscrepancy(e)
                    ? MCLDDiscreteArithmeticAPHestonEngine.Calculate(option, context, process, antithetic, Samples, null, null, seed, steps, perYear)
                    : MCPRDiscreteArithmeticAPHestonEngine.Calculate(option, context, process, antithetic, Samples, null, null, seed, steps, perYear);
            });
        }

        check.Complete();
    }

    private static int? NullableInt(JsonElement spec, string name) =>
        spec.GetProperty(name).ValueKind == JsonValueKind.Null ? null : spec.GetProperty(name).GetInt32();
}
