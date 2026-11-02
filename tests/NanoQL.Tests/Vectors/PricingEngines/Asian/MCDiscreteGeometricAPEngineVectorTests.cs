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
public class MCDiscreteGeometricAPEngineVectorTests
{
    // the generator's samples
    private const int Samples = 511;

    [Fact]
    public void MCDiscreteGeometricAPEngineVector()
    {
        var file = VectorFile.Load("pricingengines/asian/mc_discr_geom_av_price");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            JsonElement e = c.Input("engine");
            CheckResults(check, c, () =>
            {
                var option = DiscreteOptionOf(c.Input("option"), context);
                bool bridge = e.GetProperty("brownianBridge").GetBoolean(), antithetic = e.GetProperty("antitheticVariate").GetBoolean();
                long seed = e.GetProperty("seed").GetInt64();
                return LowDiscrepancy(e)
                    ? MCLDDiscreteGeometricAPEngine.Calculate(option, context, process, bridge, antithetic, Samples, null, null, seed)
                    : MCPRDiscreteGeometricAPEngine.Calculate(option, context, process, bridge, antithetic, Samples, null, null, seed);
            });
        }

        check.Complete();
    }
}
