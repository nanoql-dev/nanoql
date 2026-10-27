// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Models.EvolutionDescriptionFunctions;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The EvolutionDescription vectors, <c>tests/vectors/models/marketmodels/evolutiondescription.json</c>:
/// <see cref="EvolutionDescription"/>'s members on several grids and evolutions, the three measures, their tests and
/// <see cref="EvolutionDescriptionFunctions.CheckCompatibility"/>, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EvolutionDescriptionVectorTests
{
    [Fact]
    public void EvolutionDescriptionVector()
    {
        var file = VectorFile.Load("models/marketmodels/evolutiondescription");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = c.Doubles("rateTimes");
            switch (c.String("call"))
            {
                case "description":
                {
                    var e = new EvolutionDescription(times, c.Doubles("evolutionTimes"));
                    Members(check, c, e);
                    int n = e.NumberOfRates;
                    check.Output(c, "terminalMeasure", TerminalMeasure(e));
                    check.Output(c, "moneyMarketMeasure", MoneyMarketMeasure(e));
                    int[] offsets = [.. new SortedSet<int> { 0, 1, 2, n }.Where(o => o <= n)];
                    check.Output(c, "moneyMarketPlusMeasures", offsets.Select(o => MoneyMarketPlusMeasure(e, o)).ToArray());
                    check.Output(c, "offsetPastLast", Lazy(() => MoneyMarketPlusMeasure(e, n + 1)));
                    check.Output(c, "testOffsetPastLast", Lazy(() => IsInMoneyMarketPlusMeasure(e, TerminalMeasure(e).AsSpan(), n + 1)));
                    int[][] sets = [.. c.Input("numeraires").EnumerateArray().Select(s => Integers(s.GetProperty("numeraires")))];
                    check.Output(c, "isInTerminalMeasure", sets.Select(s => IsInTerminalMeasure(e, s)).ToArray());
                    check.Output(c, "isInMoneyMarketMeasure", sets.Select(s => IsInMoneyMarketMeasure(e, s)).ToArray());
                    check.Output(
                        c,
                        "isInMoneyMarketPlusMeasure",
                        sets.Select(s => Enumerable.Range(0, n + 1).Select(o => IsInMoneyMarketPlusMeasure(e, s, o)).ToArray()).ToArray());
                    check.Output(
                        c,
                        "checkCompatibility",
                        sets.Select(s => Lazy(() =>
                        {
                            CheckCompatibility(e, s);
                            return true;
                        })).ToArray());
                    check.Output(c, "shortCompatibility", Lazy(() =>
                    {
                        CheckCompatibility(e, TerminalMeasure(e).AsSpan()[..^1]);
                        return true;
                    }));
                    break;
                }

                case "relevance":
                    Members(check, c, new EvolutionDescription(times, default, Pairs(c.Input("relevanceRates"))));
                    break;
                case "badRelevance":
                    check.Output(c, "construction", Lazy(() => new EvolutionDescription(times, default, Pairs(c.Input("relevanceRates")))));
                    break;
                default:
                    check.Output(c, "construction", Lazy(() => new EvolutionDescription(times, c.Doubles("evolutionTimes"))));
                    break;
            }
        }

        check.Complete();
    }

    private static void Members(VectorAssert check, VectorCase c, EvolutionDescription e)
    {
        check.Output(c, "rateTimes", e.RateTimes);
        check.Output(c, "rateTaus", e.RateTaus);
        check.Output(c, "evolutionTimes", e.EvolutionTimes);
        check.Output(c, "firstAliveRate", e.FirstAliveRate);
        check.Output(c, "relevanceRates", e.RelevanceRates.Select(p => new[] { p.First, p.Second }).ToArray());
        check.Output(c, "numberOfRates", e.NumberOfRates);
        check.Output(c, "numberOfSteps", e.NumberOfSteps);
    }

    private static (int First, int Second)[] Pairs(JsonElement e) =>
        [.. e.EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32()))];
}
