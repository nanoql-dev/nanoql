// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model differences vectors,
/// <c>tests/vectors/cxx/models/marketmodels/marketmodeldifferences.json</c>:
/// <see cref="MarketModelDifferencesFunctions"/>' volatility and instantaneous volatility differences of FlatVol and
/// AbcdVol models evolved on their rate times, at each rate, and the refusals of models that don't match.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarketModelDifferencesCxxVectorTests
{
    [Fact]
    public void MarketModelDifferencesCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/marketmodeldifferences");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel m1 = ModelOf(c.Input("model1")), m2 = ModelOf(c.Input("model2"));
            check.Output(c, "rateVolDifferences", Lazy(() => MarketModelDifferencesFunctions.RateVolDifferences(m1, m2)));
            System.Text.Json.JsonElement inst = c.Expected.GetProperty("rateInstVolDifferences");
            if (inst.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                check.Output(
                    c,
                    "rateInstVolDifferences",
                    Enumerable.Range(0, inst.GetArrayLength())
                        .Select(i => MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m2, i))
                        .ToArray());
            }
            else
            {
                check.Output(c, "rateInstVolDifferences", Lazy(() => MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m2, 0)));
            }
        }

        check.Complete();
    }
}
