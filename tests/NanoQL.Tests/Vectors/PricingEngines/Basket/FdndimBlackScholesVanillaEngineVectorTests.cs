// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdndimBlackScholesVanillaEngine vectors,
/// <c>tests/vectors/pricingengines/basket/fdndimblackscholesvanillaengine.json</c>:
/// <see cref="FdndimBlackScholesVanillaEngine"/>'s NPV for European and American options on one, two and three
/// processes, on one grid size and on one per direction, under Douglas's and Hundsdorfer's schemes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdndimBlackScholesVanillaEngineVectorTests
{
    [Fact]
    public void FdndimBlackScholesVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/basket/fdndimblackscholesvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BasketOption option = BasketOptionOf(c.Input("option"), context);
            JsonElement e = c.Input("engine");
            ImmutableArray<int> grids = [.. e.GetProperty("xGrids").EnumerateArray().Select(g => g.GetInt32())];
            MultiAssetOptionResults r = grids.Length == 1
                ? FdndimBlackScholesVanillaEngine.Calculate(
                    option,
                    context,
                    ProcessesOf(e.GetProperty("processes"), context),
                    MatrixOf(e.GetProperty("rho")),
                    grids[0],
                    e.GetProperty("tGrid").GetInt32(),
                    e.GetProperty("dampingSteps").GetInt32(),
                    SchemeOf(e.GetProperty("scheme")))
                : FdndimBlackScholesVanillaEngine.Calculate(
                    option,
                    context,
                    ProcessesOf(e.GetProperty("processes"), context),
                    MatrixOf(e.GetProperty("rho")),
                    grids,
                    e.GetProperty("tGrid").GetInt32(),
                    e.GetProperty("dampingSteps").GetInt32(),
                    SchemeOf(e.GetProperty("scheme")));
            check.Output(c, "NPV", r.NPV);
        }

        check.Complete();
    }
}
