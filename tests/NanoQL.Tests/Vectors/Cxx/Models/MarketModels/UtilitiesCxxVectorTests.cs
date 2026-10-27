// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's market-model utility vectors, <c>tests/vectors/cxx/models/marketmodels/utilities.json</c>:
/// <see cref="UtilitiesFunctions"/>' merged times and presences, subset tests and time checks.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UtilitiesCxxVectorTests
{
    [Fact]
    public void UtilitiesCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/utilities");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("call"))
            {
                case "merge":
                {
                    ImmutableArray<double>[] sets = [.. c.Input("times").EnumerateArray().Select(s => ImmutableArray.Create(Reals(s)))];
                    MergedTimes merged = UtilitiesFunctions.MergeTimes(sets);
                    check.Output(c, "mergedTimes", merged.Times);
                    check.Output(c, "isPresent", merged.IsPresent);
                    break;
                }

                case "subset":
                {
                    double[] set = c.Doubles("set"), subset = c.Doubles("subset");
                    check.Output(c, "isInSubset", Lazy(() => UtilitiesFunctions.IsInSubset(set, subset)));
                    break;
                }

                default:
                {
                    double[] times = c.Doubles("times");
                    check.Output(c, "checkIncreasingTimes", Lazy(() =>
                    {
                        UtilitiesFunctions.CheckIncreasingTimes(times);
                        return true;
                    }));
                    check.Output(c, "taus", Lazy(() => UtilitiesFunctions.CheckIncreasingTimesAndCalculateTaus(times)));
                    break;
                }
            }
        }

        check.Complete();
    }
}
