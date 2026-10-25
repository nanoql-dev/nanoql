// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdBatesVanillaEngine vectors, <c>tests/vectors/pricingengines/vanilla/fdbatesvanillaengine.json</c>: the NPV,
/// delta, gamma and theta of European and American calls and puts on three Bates parameter sets, and on the second:
/// QuantLib's default grid, damping steps, every named scheme, a cash-or-nothing payoff, cash dividends, and an
/// expired option; each priced through the overload QuantLib's constructor matches.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdBatesVanillaEngineVectorTests
{
    [Fact]
    public void FdBatesVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdbatesvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var priced = new Lazy<OneAssetOptionResults>(() => Price(c.Input("pricing"), context));
            (string Name, Func<OneAssetOptionResults, double?> Get)[] results =
            [
                ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
            ];
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in results)
            {
                check.Output(c, name, () => get(priced.Value));
            }
        }

        check.Complete();
    }

    private static OneAssetOptionResults Price(JsonElement spec, EvaluationContext context)
    {
        var model = new BatesModel(BatesProcessVectorTests.BatesProcessOf(spec.GetProperty("process")));
        var option = new VanillaOption(PayoffOf(spec.GetProperty("payoff")), FdExerciseOf(spec.GetProperty("exercise"), context));
        ImmutableArray<Dividend> dividends = DividendsOf(spec.GetProperty("dividends"));
        int tGrid = spec.GetProperty("tGrid").GetInt32();
        int xGrid = spec.GetProperty("xGrid").GetInt32();
        int vGrid = spec.GetProperty("vGrid").GetInt32();
        int damping = spec.GetProperty("dampingSteps").GetInt32();
        FdmSchemeDesc scheme = SchemeOf(spec.GetProperty("scheme"));
        return dividends.IsEmpty
            ? FdBatesVanillaEngine.Calculate(option, context, model, tGrid, xGrid, vGrid, damping, scheme)
            : FdBatesVanillaEngine.Calculate(option, context, model, dividends, tGrid, xGrid, vGrid, damping, scheme);
    }
}
