// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The QdFpAmericanEngine vectors, <c>tests/vectors/pricingengines/vanilla/qdfpamericanengine.json</c>: the NPVs of
/// American calls and puts over S-van's grid, priced by the engine's three named schemes, the fast scheme on each
/// fixed-point equation and a scheme of each kind of its own sizes, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QdFpAmericanEngineVectorTests
{
    [Fact]
    public void QdFpAmericanEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/qdfpamericanengine",
            (c, option, context, process) =>
            {
                JsonElement spec = c.Input("engine");
                return QdFpAmericanEngine.Calculate(
                    option,
                    context,
                    process,
                    SchemeOf(spec.GetProperty("scheme")),
                    (QdFpAmericanEngineFixedPointEquation)spec.GetProperty("fpEquation").GetInt32());
            });

    private static QdFpIterationScheme SchemeOf(JsonElement spec) => spec.GetProperty("type").GetString() switch
    {
        "fast" => QdFpAmericanEngine.FastScheme(),
        "accurate" => QdFpAmericanEngine.AccurateScheme(),
        "highPrecision" => QdFpAmericanEngine.HighPrecisionScheme(),
        "Legendre" => new QdFpLegendreScheme(Int(spec, "l"), Int(spec, "m"), Int(spec, "n"), Int(spec, "p")),
        "LegendreTanhSinh" => new QdFpLegendreTanhSinhScheme(
            Int(spec, "l"), Int(spec, "m"), Int(spec, "n"), spec.GetProperty("eps").GetDouble()),
        "TanhSinh" => new QdFpTanhSinhIterationScheme(Int(spec, "m"), Int(spec, "n"), spec.GetProperty("eps").GetDouble()),
        string s => throw new InvalidOperationException($"unknown scheme '{s}'"),
        null => throw new InvalidOperationException("no scheme type"),
    };

    private static int Int(JsonElement spec, string name) => spec.GetProperty(name).GetInt32();
}
