// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Fd2dBlackScholesVanillaEngine vectors,
/// <c>tests/vectors/pricingengines/basket/fd2dblackscholesvanillaengine.json</c>:
/// <see cref="Fd2dBlackScholesVanillaEngine"/>'s NPV, delta, gamma and theta for European and American spread,
/// minimum, maximum and average options on two processes, under three schemes and with the local volatility.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Fd2dBlackScholesVanillaEngineVectorTests
{
    [Fact]
    public void Fd2dBlackScholesVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/fd2dblackscholesvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<GeneralizedBlackScholesProcess> p = ProcessesOf(e.GetProperty("processes"), context);
            JsonElement overwrite = e.GetProperty("illegalLocalVolOverwrite");
            MultiAssetOptionResults r = Fd2dBlackScholesVanillaEngine.Calculate(
                option,
                context,
                p[0],
                p[1],
                e.GetProperty("correlation").GetDouble(),
                e.GetProperty("xGrid").GetInt32(),
                e.GetProperty("yGrid").GetInt32(),
                e.GetProperty("tGrid").GetInt32(),
                e.GetProperty("dampingSteps").GetInt32(),
                SchemeOf(e.GetProperty("scheme")),
                e.GetProperty("localVol").GetBoolean(),
                overwrite.ValueKind == JsonValueKind.Null ? null : overwrite.GetDouble());
            check.Output(c, "NPV", r.NPV);
            check.Output(c, "delta", r.Delta);
            check.Output(c, "gamma", r.Gamma);
            check.Output(c, "theta", r.Theta);
        }

        check.Complete();
    }
}
