// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's period adapter vectors, <c>tests/vectors/cxx/models/marketmodels/models/fwdperiodadapter.json</c>:
/// <see cref="FwdPeriodAdapter"/> on FlatVol models of twelve semi-annual and nine quarterly rates, in periods of 2, 3
/// and 4 at several offsets, with one displacement given and with one per long rate: the long initial rates and
/// displacements, the evolution's rate times, evolution times and first alive rates, and the counts; the pseudo-roots,
/// on QuantLib's Y matrix (the S-mm plan's MR3 (i)), and the default displacements (MR3 (ii)) are the Divergences
/// record's; and the refused adapters.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FwdPeriodAdapterCxxVectorTests
{
    [Fact]
    public void FwdPeriodAdapterCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/fwdperiodadapter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            MarketModel model = ModelOf(c.Input("model"));
            if (c.String("call") == "refused")
            {
                foreach (JsonProperty r in c.Input("refusals").EnumerateObject())
                {
                    JsonElement[] a = [.. r.Value.EnumerateArray()];
                    check.Output(c, r.Name, Lazy(() => new FwdPeriodAdapter(model, a[0].GetInt32(), a[1].GetInt32(), Reals(a[2]))));
                }

                MarketModel skip = ModelOf(c.Input("skipModel"));
                check.Output(c, "skippedLongRateTime", Lazy(() => new FwdPeriodAdapter(skip, 3, 0, [0.01])));
                continue;
            }

            var adapter = new FwdPeriodAdapter(model, c.Int("period"), c.Int("offset"), c.Doubles("newDisplacements"));
            check.Output(c, "initialRates", adapter.InitialRates);
            check.Output(c, "displacements", adapter.Displacements);
            check.Output(c, "rateTimes", adapter.Evolution.RateTimes);
            check.Output(c, "evolutionTimes", adapter.Evolution.EvolutionTimes);
            check.Output(c, "firstAliveRate", adapter.Evolution.FirstAliveRate);
            check.Output(c, "numberOfRates", adapter.NumberOfRates);
            check.Output(c, "numberOfFactors", adapter.NumberOfFactors);
            check.Output(c, "numberOfSteps", adapter.NumberOfSteps);
        }

        check.Complete();
    }
}
