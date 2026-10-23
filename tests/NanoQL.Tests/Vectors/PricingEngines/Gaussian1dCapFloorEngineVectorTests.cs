// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's Gaussian1d cap/floor engine vectors,
/// <c>tests/vectors/pricingengines/capfloor/gaussian1dcapfloorengine.json</c>: caps, floors and collars on Euribor,
/// every caplet fixing after the evaluation date, on three GSR models, the engine's grid and extrapolation settings
/// varied on the first, and the optionlets' prices.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dCapFloorEngineVectorTests
{
    [Fact]
    public void Gaussian1dCapFloorEngineVector()
    {
        var file = VectorFile.Load("pricingengines/capfloor/gaussian1dcapfloorengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloor capFloor = CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
            Gsr[] models = [.. c.Input("models").EnumerateArray().Select(Gaussian1dInputs.GsrOf)];
            check.Output(c, "gsr", models.Select(m => (Func<object?>)(() => Gaussian1dCapFloorEngine.Calculate(capFloor, context, m).NPV)));
            check.Output(
                c,
                "settings",
                c.Input("engineSettings").EnumerateArray().Select(s => (Func<object?>)(() => Price(capFloor, context, models[0], s))));
            check.Output(
                c, "optionletsPrice", Gaussian1dCapFloorEngine.Calculate(capFloor, context, models[0]).Result<ImmutableArray<double>>("optionletsPrice"));
        }

        check.Complete();
    }

    private static double? Price(CapFloor capFloor, EvaluationContext context, Gsr model, JsonElement s) =>
        Gaussian1dCapFloorEngine.Calculate(
            capFloor,
            context,
            model,
            s.GetProperty("integrationPoints").GetInt32(),
            s.GetProperty("stddevs").GetDouble(),
            s.GetProperty("extrapolatePayoff").GetBoolean()).NPV;
}
